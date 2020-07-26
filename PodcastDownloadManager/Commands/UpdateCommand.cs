using System;
using System.Collections.Generic;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class UpdateCommand
    {
        public const string Name = "update";
        public const string Description = "Update the podcast newly release.";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            output.WriteLine("Updating...");
            Opml.UpdateAllPodcasts(out var outputList);

            for (int i = 0; i < outputList.Count; i++)
            {
                output.WriteLine(outputList[i]);
            }
            output.WriteLine("pdlm was updated successfully!");
            return 0;
        }
    }
}