﻿using NFlags;
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
            Logger.Log.Info("Enter Show command.");

            string podcastName = commandArgs.GetParameter<string>(PodcastName);

            Logger.Log.Info($"Get the Show command input string, is {podcastName}.");

            string detail = Opml.ShowPodcastDetail(podcastName);

            Logger.Log.Info("Show the podcast detail results.");
            output.WriteLine(detail);
            return 0;
        }
    }
}