using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Xml.Linq;

using DotGet.Core.Configuration;
using DotGet.Core.Logging;

using Newtonsoft.Json;

namespace DotGet.Core.Resolvers
{
    internal class NuGetPackageResolver : Resolver
    {
        private class VersionResponse
        {
            public string[] Versions { get; set; }
        }

        private readonly string _baseUrl = "https://api.nuget.org/v3-flatcontainer/";

        public NuGetPackageResolver(string source, ResolutionType resolutionType, ILogger logger) : base(source, resolutionType, logger) { }

        public override ResolverOptions BuildOptions()
        {
            ResolverOptions options = new ResolverOptions();

            string[] parts = Source.Split('@');
            options.Add("package", parts[0]);

            if (parts.Length > 1)
                options.Add("version", parts[1].ToLowerInvariant());

            return options;
        }

        public override bool CanResolve()
            => !Source.Contains("/") && !Source.Contains(@"\") && !Source.StartsWith(".");

        public override bool CheckInstalled()
        {
            if (ResolutionType == ResolutionType.Install)
                return Directory.Exists(Path.Combine(SpecialFolders.Lib, ResolverOptions["package"]));

            return Directory.Exists(Path.Combine(SpecialFolders.Lib, Source));
        }

        public override bool Resolve()
        {
            string url = _baseUrl;
            string package = ResolverOptions["package"].ToLowerInvariant();
            string version = string.Empty;
            var webClient = new WebClient();

            if (!ResolverOptions.TryGetValue("version", out version))
            {
                try
                {
                    string json = webClient.DownloadString(_baseUrl + package + "/" + "index.json");
                    VersionResponse response = JsonConvert.DeserializeObject<VersionResponse>(json);
                    version = response.Versions.Last();
                }
                catch
                {
                    return false;
                }
            }

            url += package + "/" + version + "/" + package + "." + version + ".nupkg";

            if (ResolutionType == ResolutionType.Update)
            {
                if (version == GetInstalledPackageVersion())
                    return false;
            }

            string nupkg = Path.Combine(Path.GetTempPath(), package + "." + version + ".nupkg");
            string packageDirectory = Path.Combine(SpecialFolders.Lib, package);

            try
            {
                webClient.DownloadFile(url, nupkg);
            }
            catch
            {
                return false;
            }

            if (ResolutionType == ResolutionType.Update)
                Directory.Delete(packageDirectory, true);

            ZipFile.ExtractToDirectory(nupkg, packageDirectory);

            string toolsDirectory = Path.Combine(packageDirectory, "tools");
            if (!Directory.Exists(toolsDirectory))
                return false;

            string appDirectory = Directory
                                    .GetDirectories(toolsDirectory, "netcoreapp*", SearchOption.TopDirectoryOnly)
                                    .LastOrDefault();

            if (appDirectory == null)
                return false;

            var executables = GetExecutableAssemblies(appDirectory);
            foreach (var executable in executables)
                CreatePlatformExecutable(executable);

            return true;
        }

        public override bool Remove()
        {
            throw new NotImplementedException();
        }

        private IEnumerable<string> GetExecutableAssemblies(string appDirectory)
        {

            string[] assemblies = Directory.GetFiles(appDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            AssemblyLoadContext assemblyLoadContext = AssemblyLoadContext.Default;

            foreach (var assembly in assemblies)
            {
                if (assemblyLoadContext.LoadFromAssemblyPath(assembly).EntryPoint != null)
                    yield return assembly;
            }

        }

        private string GetInstalledPackageVersion()
        {
            string package = ResolverOptions["package"];
            string xml = File.ReadAllText(Path.Combine(SpecialFolders.Lib, package, $"{package}.nuspec"));
            XElement root = XDocument.Parse(xml).Root;
            string @namespace = root.GetDefaultNamespace().NamespaceName;
            return root.Element(XName.Get("metadata", @namespace)).Element(XName.Get("version", @namespace)).Value;
        }

        private void CreatePlatformExecutable(string executable)
        {
            string command = Path.GetFileNameWithoutExtension(executable);
            string contents = string.Empty;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                command += ".cmd";
                contents = $"dotnet {executable} %*";
            }
            else
            {
                contents = "#!/usr/bin/env bash";
                contents += Environment.NewLine;
                contents += $"dotnet {executable} $@";
            }

            File.WriteAllText(Path.Combine(SpecialFolders.Bin, command), contents);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process process = new Process();
                process.StartInfo.FileName = "chmod";
                process.StartInfo.Arguments = $"+x {Path.Combine(SpecialFolders.Bin, command)}";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                process.WaitForExit();
            }
        }
    }
}