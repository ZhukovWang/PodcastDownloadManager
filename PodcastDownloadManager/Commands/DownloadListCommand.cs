using System;
using System.Collections.Generic;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class DownloadListCommand
    {
        public const string Name = "list";
        public const string Description = "Show the podcast all release and choose to download.";

        private const string PodcastName = "Name";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterParameter(PodcastName, "Specific a podcast and show all releases", "None")
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            output.WriteLine("Listing...");

            string name = commandArgs.GetParameter<string>(PodcastName);

            List<string> outputList = new List<string>();

            int res = Opml.ListPodcastAllRelease(name, ref outputList);

            if (res == 0)
            {
                for (var i = 0; i < outputList.Count; i++)
                {
                    output.Write((i + 1).ToString() + ". ");
                    output.Write(outputList[i]);
                }

                output.WriteLine("Done.");
            }
            else
            {
                output.WriteLine(outputList[0]);
            }
            return 0;
        }
    }
}