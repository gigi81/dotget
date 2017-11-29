﻿using DotGet.Cli.Logging;
using DotGet.Core.Commands;
using Microsoft.Extensions.CommandLineUtils;

namespace DotGet.Cli
{
    class Program
    {
        static int Main(string[] args)
        {
            Logger logger = new Logger() { Level = LogLevel.Error | LogLevel.Info | LogLevel.Success | LogLevel.Warning };
            var app = new CommandLineApplication();
            app.Name = "dotget";
            app.FullName = ".NET Core Tools Global Installer";
            app.Description = "Install and use command line tools built on .NET Core";
            app.HelpOption("-h|--help");
            app.VersionOption("-v|--version", "1.0.0");

            CommandOption verboseOption = app.Option("--verbose", "Enable verbose output", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });

            app.Command("install", c =>
            {
                c.Description = "Installs a .NET Core tool";
                c.HelpOption("-h|--help");

                CommandArgument toolArg = c.Argument("<TOOL>", "The tool to install. Can be a NuGet package");

                c.OnExecute(() =>
                {
                    if (string.IsNullOrWhiteSpace(toolArg.Value))
                    {
                        logger.LogError("<TOOL> argument is required. Use -h|--help to see help");
                        return 1;
                    }

                    UpdateLoggerIfVerbose(verboseOption, logger);
                    InstallCommand installCommand = new InstallCommand(toolArg.Value, logger);
                    installCommand.Execute();
                    return 0;
                });
            });

            app.Command("upgrade", c =>
            {
                c.Description = "Upgrades a .NET Core tool";
                c.HelpOption("-h|--help");

                CommandArgument toolArg = c.Argument("<TOOL>", "The tool to update.");

                c.OnExecute(() =>
                {
                    if (string.IsNullOrWhiteSpace(toolArg.Value))
                    {
                        logger.LogError("<TOOL> argument is required. Use -h|--help to see help");
                        return 1;
                    }

                    UpdateLoggerIfVerbose(verboseOption, logger);
                    UpgradeCommand upgradeCommand = new UpgradeCommand(toolArg.Value, logger);
                    upgradeCommand.Execute();
                    return 0;
                });
            });

            app.Command("list", c => 
            {
                c.Description = "Lists all installed .NET Core tools";
                c.HelpOption("-h|--help");

                c.OnExecute(() =>
                {
                    ListCommand listCommand = new ListCommand(logger);
                    listCommand.Execute();
                    return 0;
                });
            });

            app.Command("uninstall", c =>
            {
                c.Description = "Uninstalls a .NET Core tool";
                c.HelpOption("-h|--help");

                CommandArgument toolArg = c.Argument("<TOOL>", "The tool to uninstall.");

                c.OnExecute(() =>
                {
                    if (string.IsNullOrWhiteSpace(toolArg.Value))
                    {
                        logger.LogError("<TOOL> argument is required. Use -h|--help to see help");
                        return 1;
                    }

                    UpdateLoggerIfVerbose(verboseOption, logger);
                    UninstallCommand uninstallCommand = new UninstallCommand(toolArg.Value, logger);
                    uninstallCommand.Execute();
                    return 0;
                });
            });

            try
            {
                return app.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                logger.LogWarning(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }

        static void UpdateLoggerIfVerbose(CommandOption verboseOption, Logger logger)
        {
            if (verboseOption.HasValue())
                logger.Level = logger.Level | LogLevel.Verbose;
        }
    }
}
