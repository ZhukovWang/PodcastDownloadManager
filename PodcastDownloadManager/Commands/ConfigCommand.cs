using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class ConfigCommand
    {
        public const string Name = "config";
        public const string Description = "Get or set configuration values.";

        private const string DownloadPodcastPath = "download-path";
        private const string DownloadProgram = "download-program";
        private const string DownloadProgramPath = "download-program-path";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterOption(DownloadPodcastPath, "p", "The downloaded podcast saved path.", ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath)
                .RegisterOption(DownloadProgram, "dp", $"The downloaded program name, now support are {DownloadTools.Aria2Name} and {DownloadTools.IdmName}.", ProgramConfiguration.DownloadConfigurations.DownloadProgram)
                .RegisterOption(DownloadProgramPath, "dpp", $"The downloaded program path.", ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName)
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath =
                commandArgs.GetOption<string>(DownloadPodcastPath);
            ProgramConfiguration.DownloadConfigurations.DownloadProgram =
                commandArgs.GetOption<string>(DownloadProgram);
            ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName =
                commandArgs.GetOption<string>(DownloadProgramPath);

            ProgramConfiguration.SaveConfig();

            output.WriteLine("Done.");

            return 0;
        }
    }
}