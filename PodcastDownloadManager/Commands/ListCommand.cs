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
            Logger.Log.Info("Enter List command.");

            Logger.Log.Info("Show all podcast info.");

            output.WriteLine("Podcasts:");

            Opml.ListPodcast(ref output);

            return 0;
        }
    }
}