using System;
using System.IO;
using NFlags;
using NFlags.Commands;

namespace PodcastDownloadManager
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            string podcastsFileDirectory = "PodcastsFile";
            if (!Directory.Exists(podcastsFileDirectory))
            {
                Directory.CreateDirectory(podcastsFileDirectory);
            }

            Cli.Configure(ConfigureNFlags)
                .Root(RootCommand.Configure)
                .Run(args);
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
    }
}