using System;
using System.Collections.Generic;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class ListCommand
    {
        public const string Name = "list";
        public const string Description = "List added podcasts.";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            List<string> list;

            Opml.ListPodcast(out list);

            output.WriteLine("Podcasts:");

            foreach (string s in list)
            {
                output.Write(s);
            }

            return 0;
        }
    }
}