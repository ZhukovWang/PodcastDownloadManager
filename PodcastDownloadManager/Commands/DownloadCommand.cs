using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.IO;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class DownloadCommand
    {
        public const string Name = "download";
        public const string Description = "Download the podcast newly release.";

        private const string Date = "date";
        private const string DownloadDirectory = "dir";
        private const string SimpleFile = "simple-file";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterParameter(Date, "Release will be downloaded after the date.", "20200701")
                .RegisterFlag(SimpleFile, "s", "Create a simple file just have download url, could use to other download software. And not automatic download.", false)
                .RegisterOption(DownloadDirectory, "d", "Download file directory.", ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath)
                .RegisterCommand(DownloadListCommand.Name, DownloadListCommand.Description, DownloadListCommand.Configure)
                .RegisterCommand(DownloadSelectCommand.Name, DownloadSelectCommand.Description, DownloadSelectCommand.Configure)
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            string podcastsDownloadDirectory = commandArgs.GetOption<string>(DownloadDirectory);
            bool isSimpleFile = commandArgs.GetFlag(SimpleFile);

            output.WriteLine("Building downloading file...");

            if (!Directory.Exists(podcastsDownloadDirectory))
            {
                Directory.CreateDirectory(podcastsDownloadDirectory);
            }

            string url = commandArgs.GetParameter<string>(Date);
            DateTime dt = DateTime.ParseExact(url, "yyyyMMdd", new CultureInfo("en-US"));
            Opml.DownloadPodcastAfterDate(dt, podcastsDownloadDirectory, isSimpleFile, ProgramConfiguration.DownloadConfigurations.DownloadProgram);

            output.WriteLine("Building done");

            if (!isSimpleFile)
            {
                output.WriteLine("Downloading...");

                if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.Aria2Name)
                {
                    DownloadTools.DownloadAria2(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.DownloadFileName, output);
                }
                else if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.IdmName)
                {
                    DownloadTools.DownloadIdm(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.DownloadFileName, output);
                }

                output.WriteLine("Done.");
            }
            return 0;
        }
    }
}