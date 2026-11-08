using NFlags;
using System;
using System.IO;

namespace PodcastDownloadManager
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                ProgramLoaded();
                Logger.Log.Info("Program load finish.");

                int exitCode = Cli.Configure(ConfigureNFlags)
                    .Root(RootCommand.Configure)
                    .Run(args);

                Logger.Log.Info($"Program exit with code {exitCode}.");

                return exitCode;
            }
            catch (Exception e)
            {
                Logger.Log.Error($"Program terminated unexpectedly. Exception is {e}");
                Console.Error.WriteLine($"pdlm failed: {e.Message}");

                return ExitCode.Failure;
            }
        }

        private static void ConfigureNFlags(CliConfigurator configurator)
        {
            configurator
                .SetName("pdlm")
                .SetDescription("Podcast Download Manager is a commandline podcast manager and provides commands\nfor managing and downloading podcast.")
                .SetDialect(Dialect.Gnu)
                .SetOutput(Output.Console)
                .ConfigureVersion(vc => vc.Enable());
        }

        private static void ProgramLoaded()
        {
            if (!Directory.Exists(ProgramConfiguration.PodcastsFileDirectory))
            {
                Directory.CreateDirectory(ProgramConfiguration.PodcastsFileDirectory);
                Logger.Log.Info($"Create PodcastsFileDirectory, directory locate {ProgramConfiguration.PodcastsFileDirectory}");
            }

            if (File.Exists(ProgramConfiguration.ConfigFilePathName))
            {
                ProgramConfiguration.ReadConfig();
                Logger.Log.Info("Read config file.");
            }
            else
            {
                ProgramConfiguration.CreateConfig();
                Logger.Log.Info("Create new config file.");
            }
        }
    }
}