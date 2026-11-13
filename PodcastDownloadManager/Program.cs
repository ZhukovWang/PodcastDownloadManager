using NFlags;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PodcastDownloadManager
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                // NFlags would print the assembly version (1.0.0.0), which does not include the
                // git description injected at build time. Handle the version request here so both
                // the banner and '--version' show the same value.
                if (IsVersionRequest(args))
                {
                    PrintVersion();
                    return ExitCode.Success;
                }

                PrintVersion();
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
                OutputTools.WriteConsoleError($"pdlm failed: {e.Message}");

                return ExitCode.Failure;
            }
        }

        private static void PrintVersion()
        {
            Console.WriteLine($"pdlm {GetVersion()}");
        }

        private static string GetVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            AssemblyInformationalVersionAttribute informationalVersion =
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            if (!string.IsNullOrWhiteSpace(informationalVersion?.InformationalVersion))
            {
                return informationalVersion.InformationalVersion;
            }

            return assembly.GetName().Version?.ToString() ?? "unknown";
        }

        private static bool IsVersionRequest(string[] args)
        {
            return args.Any(argument => argument == "-v" || argument == "--version");
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