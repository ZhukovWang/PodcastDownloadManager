using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;
using System;
using System.Globalization;
using System.IO;

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
                .RegisterParameter(Date, "Release will be downloaded after the date.", "20210101")
                .RegisterFlag(SimpleFile, "s", "Create a simple file just have download url, could use to other download software. And not automatic download.", false)
                .RegisterOption(DownloadDirectory, "d", "Download file directory.", ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath)
                .RegisterCommand(DownloadListCommand.Name, DownloadListCommand.Description, DownloadListCommand.Configure)
                .RegisterCommand(DownloadSelectCommand.Name, DownloadSelectCommand.Description, DownloadSelectCommand.Configure)
                .SetExecute(Execute);
        }

        /// <summary>
        /// Parses the command's <c>yyyyMMdd</c> date as local midnight and converts it to UTC, so it
        /// lines up with the UTC publication dates stored by the feed parser.
        /// </summary>
        internal static bool TryParseDate(string value, out DateTime utc)
        {
            if (!DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime localDate))
            {
                utc = default;
                return false;
            }

            DateTime local = DateTime.SpecifyKind(localDate, DateTimeKind.Local);
            utc = local.ToUniversalTime();
            return true;
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
                    return ExitCode.Failure;
                }
            }

            bool isSimpleFile = commandArgs.GetFlag(SimpleFile);

            Logger.Log.Info($"Download simple file is {isSimpleFile}.");

            string date = commandArgs.GetParameter<string>(Date);

            if (!TryParseDate(date, out DateTime dt))
            {
                Logger.Log.Warn($"Download date is not valid.");

                output.WriteLine("Error. Input of 'date' is illegal.");
                return ExitCode.Failure;
            }

            Logger.Log.Info($"Download date is {dt.ToString("G")} (UTC).");

            output.WriteLine("Building downloading file...");

            Logger.Log.Info("Build download file.");

            int failedFeeds = Opml.DownloadPodcastAfterDate(dt, podcastsDownloadDirectory, isSimpleFile,
                ProgramConfiguration.DownloadConfigurations.DownloadProgram, ref output);

            Logger.Log.Info("Finish build download file.");

            output.WriteLine("Building done");

            int exitCode = failedFeeds == 0 ? ExitCode.Success : ExitCode.Failure;

            if (!isSimpleFile)
            {
                output.WriteLine("Downloading...");

                Logger.Log.Info(
                    $"Start download use {ProgramConfiguration.DownloadConfigurations.DownloadProgram}.");

                int downloadExitCode = DownloadTools.Download(
                    ProgramConfiguration.DownloadConfigurations.DownloadProgram,
                    ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName,
                    ProgramConfiguration.DownloadFileName, output);

                if (downloadExitCode != ExitCode.Success)
                {
                    Logger.Log.Warn($"The downloader exited with code {downloadExitCode}.");
                    output.WriteLine($"The downloader exited with code {downloadExitCode}.");
                    exitCode = ExitCode.Failure;
                }

                Logger.Log.Info("Finish download.");
                output.WriteLine("Done.");
            }

            return exitCode;
        }
    }
}
