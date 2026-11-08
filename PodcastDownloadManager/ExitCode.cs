namespace PodcastDownloadManager
{
    /// <summary>
    /// Exit codes returned by the CLI.
    /// </summary>
    public static class ExitCode
    {
        /// <summary>
        /// The command completed all of its work successfully.
        /// </summary>
        public const int Success = 0;

        /// <summary>
        /// The command failed, either because the input was invalid or because at least one
        /// operation (feed, downloader, tag write, ...) did not complete.
        /// </summary>
        public const int Failure = 1;
    }
}
