using System;
using System.Collections.Generic;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class ShowCommand
    {
        public const string Name = "show";
        public const string Description = "Display information about a podcast and newly release.";

        private const string PodcastName = "name";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterParameter(PodcastName, "Podcast information to show", "Podcast name is not found.")
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            string podcastName = commandArgs.GetParameter<string>(PodcastName);

            string detail = Opml.ShowPodcastDetail(podcastName);

            output.WriteLine(detail);
            return 0;
        }
    }
}