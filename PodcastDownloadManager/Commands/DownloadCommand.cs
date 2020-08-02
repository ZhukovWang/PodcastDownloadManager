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
            Logger.Log.Info("Enter Download command.");

            string podcastsDownloadDirectory = commandArgs.GetOption<string>(DownloadDirectory);

            Logger.Log.Info($"Download directory is {podcastsDownloadDirectory}.");

            if (!Directory.Exists(podcastsDownloadDirectory))
            {
                Logger.Log.Info("Create download directory.");

                try
                {
                    Directory.CreateDirectory(podcastsDownloadDirectory);
                }
                catch
                {
                    Logger.Log.Info($"Directory \"{podcastsDownloadDirectory}\" is not valid.");
                    output.WriteLine("Error. Input of 'dir' is illegal.");
                    return 0;
                }
            }

            bool isSimpleFile = commandArgs.GetFlag(SimpleFile);

            Logger.Log.Info($"Download simple file is {isSimpleFile}.");

            string date = commandArgs.GetParameter<string>(Date);
            DateTime dt;
            try
            {
                dt = DateTime.ParseExact(date, "yyyyMMdd", new CultureInfo("en-US"));

                Logger.Log.Info($"Download date is {dt.ToString("G")}.");
            }
            catch
            {
                Logger.Log.Warn($"Download date is not valid.");

                output.WriteLine("Error. Input of 'date' is illegal.");
                return 0;
            }

            output.WriteLine("Building downloading file...");

            Logger.Log.Info("Build download file.");

            Opml.DownloadPodcastAfterDate(dt, podcastsDownloadDirectory, isSimpleFile, ProgramConfiguration.DownloadConfigurations.DownloadProgram);

            Logger.Log.Info("Finish build download file.");

            output.WriteLine("Building done");

            if (!isSimpleFile)
            {
                output.WriteLine("Downloading...");

                if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.Aria2Name)
                {
                    Logger.Log.Info("Start download newly release use aria2.");

                    DownloadTools.DownloadAria2(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.DownloadFileName, output);
                }
                else if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.IdmName)
                {
                    Logger.Log.Info("Start download newly release use idm.");

                    DownloadTools.DownloadIdm(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.DownloadFileName, output);
                }

                Logger.Log.Info("Finish download.");
                output.WriteLine("Done.");
            }
            return 0;
        }
    }
}