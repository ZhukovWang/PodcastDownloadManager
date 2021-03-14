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

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            Logger.Log.Info("Enter Upgrade command.");

            if (File.GetLastWriteTimeUtc(ProgramConfiguration.PodcastNewlyReleaseInfo) - DateTime.Now > TimeSpan.FromDays(1))
            {
                Logger.Log.Info("Need Upgrade first.");

                output.WriteLine("Updating...");
                Opml.UpdateAllPodcasts(ref output);
                output.WriteLine("Update done.");
            }

            output.WriteLine("Upgrading...");

            if (File.Exists(ProgramConfiguration.PodcastNewlyReleaseInfo))
            {
                if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.Aria2Name)
                {
                    Logger.Log.Info("Start download newly release use aria2.");
                    output.WriteLine("Start download newly release use aria2.");
                    DownloadTools.DownloadAria2(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.PodcastNewlyReleaseInfo, output);
                }
                else if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.IdmName)
                {
                    Logger.Log.Info("Start download newly release use idm.");
                    output.WriteLine("Start download newly release use idm.");
                    DownloadTools.DownloadIdm(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.PodcastNewlyReleaseInfo, output);
                }
                File.Delete(ProgramConfiguration.PodcastNewlyReleaseInfo);

                Logger.Log.Info("Finish download newly release.");
                File.Delete(ProgramConfiguration.PodcastNewlyReleaseInfo);
                output.WriteLine("Done.");
            }
            else
            {
                Logger.Log.Info("Nothing upgrade.");
                output.WriteLine("All up-to-date. Nothing download.");
            }

            return 0;
        }
    }
}