using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class RemoveCommand
    {
        public const string Name = "remove";
        public const string Description = "Remove a podcast through name.";

        private const string PodcastName = "name";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterParameter(PodcastName, "The name of podcast need to remove.", "Name is not found.")
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            string podcastName = commandArgs.GetParameter<string>(PodcastName);

            if (File.Exists(ProgramConfiguration.PodcastFileName))
            {
                int res = Opml.RemovePodcast(podcastName);

                output.WriteLine(res == 0 ? "Had removed." : $"There is not podcast named {podcastName}.");
            }
            else
            {
                output.WriteLine("There is not a profile. Please at least add one podcast.");
            }
            return 0;
        }
    }
}