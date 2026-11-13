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

            int count = Opml.UpdateAllPodcasts(ref output, out int failedCount);

            if (failedCount > 0)
            {
                output.WriteError($"Update finished with {failedCount} failed feed(s).");
            }
            else
            {
                output.WriteLine(count == 0 ? "All up-to-date." : "pdlm was updated successfully!");
            }

            return failedCount == 0 ? ExitCode.Success : ExitCode.Failure;
        }
    }
}