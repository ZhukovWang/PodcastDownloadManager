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
            Logger.Log.Info("Enter Remove command.");

            string podcastName = commandArgs.GetParameter<string>(PodcastName);

            Logger.Log.Info($"Get the Remove command input string, is {podcastName}.");

            if (File.Exists(ProgramConfiguration.PodcastFileName))
            {
                Logger.Log.Info("Remove the podcast.");
                int res = Opml.RemovePodcast(podcastName);

                if (res == 0)
                {
                    Logger.Log.Info("Finish remove the podcast.");
                    output.WriteLine("Had removed.");
                }
                else
                {
                    Logger.Log.Warn($"There is not podcast named {podcastName}.");
                    output.WriteLine($"There is not podcast named {podcastName}.");
                }
            }
            else
            {
                Logger.Log.Warn("There is not a opml file.");
                output.WriteLine("There is not a profile. Please at least add one podcast.");
            }
            return 0;
        }
    }
}