using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class UpgradeCommand
    {
        public const string Name = "upgrade";
        public const string Description = "download the podcast newly release since last update.";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            if (File.GetLastWriteTimeUtc(ProgramConfiguration.PodcastNewlyReleaseInfo) - DateTime.Now > TimeSpan.FromDays(1))
            {
                output.WriteLine("Updating...");
                Opml.UpdateAllPodcasts(out var outputList);
                output.WriteLine("Update done.");
            }

            output.WriteLine("Upgrading...");

            if (File.Exists(ProgramConfiguration.PodcastNewlyReleaseInfo))
            {
                if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == ProgramConfiguration.Aria2Name)
                {
                    DownloadCommand.DownloadAria2(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.PodcastNewlyReleaseInfo, output);
                }
                else if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == ProgramConfiguration.IdmName)
                {
                    DownloadCommand.DownloadIdm(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.PodcastNewlyReleaseInfo, output);
                }
                File.Delete(ProgramConfiguration.PodcastNewlyReleaseInfo);
                output.WriteLine("Done.");
            }
            else
            {
                output.WriteLine("All up-to-date. Nothing download.");
            }

            return 0;
        }
    }
}