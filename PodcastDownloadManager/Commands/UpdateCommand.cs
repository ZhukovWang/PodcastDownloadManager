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
            Logger.Log.Info("Enter Update command.");

            //File.Delete(ProgramConfiguration.PodcastNewlyReleaseInfo);

            output.WriteLine("Updating...");
            Logger.Log.Info("Show the update results.");

            int count = Opml.UpdateAllPodcasts(ref output);

            output.WriteLine(count == 0 ? "All up-to-date." : "pdlm was updated successfully!");

            return 0;
        }
    }
}