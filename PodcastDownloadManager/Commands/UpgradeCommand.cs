using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;
using System;
using System.IO;

namespace PodcastDownloadManager.Commands
{
    public static class UpgradeCommand
    {
        public const string Name = "upgrade";
        public const string Description = "Download the podcast newly release since last update.";

        /// <summary>
        /// A pending download list older than this is rebuilt before it is handed to the downloader.
        /// </summary>
        private static readonly TimeSpan MaxDownloadListAge = TimeSpan.FromDays(1);

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .SetExecute(Execute);
        }

        /// <summary>
        /// Whether the pending download list is missing or old enough to be refreshed first.
        /// Both sides of the comparison are UTC.
        /// </summary>
        internal static bool NeedsUpdate(string downloadListPath, DateTime utcNow)
        {
            if (string.IsNullOrEmpty(downloadListPath) || !File.Exists(downloadListPath))
            {
                return true;
            }

            return File.GetLastWriteTimeUtc(downloadListPath) < utcNow - MaxDownloadListAge;
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            Logger.Log.Info("Enter Upgrade command.");

            int exitCode = ExitCode.Success;

            if (NeedsUpdate(ProgramConfiguration.PodcastNewlyReleaseInfo, DateTime.UtcNow))
            {
                Logger.Log.Info("Need Upgrade first.");

                output.WriteLine("Updating...");
                Opml.UpdateAllPodcasts(ref output, out int failedFeeds);
                output.WriteLine("Update done.");

                if (failedFeeds > 0)
                {
                    exitCode = ExitCode.Failure;
                }
            }

            output.WriteLine("Upgrading...");

            if (!File.Exists(ProgramConfiguration.PodcastNewlyReleaseInfo))
            {
                Logger.Log.Info("Nothing upgrade.");

                if (exitCode == ExitCode.Success)
                {
                    output.WriteLine("All up-to-date. Nothing download.");
                }
                else
                {
                    output.WriteError("Update failed. Nothing to download.");
                }

                return exitCode;
            }

            string downloadProgram = ProgramConfiguration.DownloadConfigurations.DownloadProgram;

            if (downloadProgram == DownloadTools.Aria2Name)
            {
                Logger.Log.Info("Start download newly release use aria2.");
                output.WriteLine("Start download newly release use aria2.");
            }
            else if (downloadProgram == DownloadTools.IdmName)
            {
                Logger.Log.Info("Start download newly release use idm.");
                output.WriteLine("Start download newly release use idm.");
            }

            int downloadExitCode = DownloadTools.Download(downloadProgram,
                ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName,
                ProgramConfiguration.PodcastNewlyReleaseInfo, output);

            if (downloadExitCode != ExitCode.Success)
            {
                Logger.Log.Warn($"The downloader exited with code {downloadExitCode}; the download list is kept.");
                output.WriteError(
                    $"The downloader exited with code {downloadExitCode}. Keeping \"{ProgramConfiguration.PodcastNewlyReleaseInfo}\" for a retry.");
                return ExitCode.Failure;
            }

            File.Delete(ProgramConfiguration.PodcastNewlyReleaseInfo);

            Logger.Log.Info("Finish download newly release.");
            output.WriteLine("Done.");

            return exitCode;
        }
    }
}
