using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using DotGet.Core.Exceptions;
using DotGet.Core.Helpers;
using DotGet.Core.Logging;
using DotGet.Core.Resolvers;

namespace DotGet.Core.Commands
{
    public class UpdateCommand : ICommand
    {
        private string _source;
        private ILogger _logger;

        public UpdateCommand(string source, ILogger logger)
        {
            _source = source;
            _logger = logger;
        }

        public bool Execute()
        {
            _logger.LogProgress($"Checking if {_source} is already installed");
            if (!CommandHelper.IsInstalled(_source))
            {
                _logger.LogResult("fail");
                return false;
            }

            _logger.LogResult("ok");
            _logger.LogProgress($"Retreiving resolver for {_source}");

            Resolver resolver = ResolverFactory.GetResolverForSource(_source);

            if (resolver == null)
            {
                _logger.LogResult("fail");
                return false;
            }

            _logger.LogResult("ok");
            string path = string.Empty;

            try
            {
                _logger.LogProgress($"Resolving {_source}");
                path = resolver.Resolve(_source, ResolutionType.Update, _logger);
                _logger.LogResult("ok");
            }
            catch (ResolverException ex)
            {
                _logger.LogResult("fail");
                _logger.LogError(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogResult("fail");
                 _logger.LogVerbose(ex.ToString());
                return false;
            }

            _logger.LogInformation($"Updating executable for {_source}");
            string filename = Path.Combine(Globals.GlobalBinDirectory, CommandHelper.BuildBinFilename(path));
            File.WriteAllText(filename, CommandHelper.BuildBinContents(path));
            return true;
        }
    }
}