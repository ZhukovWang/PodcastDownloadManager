using NFlags;
using NFlags.Commands;
using System;
using System.Globalization;
using System.IO;

namespace PodcastDownloadManager.Commands
{
    public static class ConfigCommand
    {
        public const string Name = "config";
        public const string Description = "Set configuration values.";

        private const string DownloadPodcastPath = "download-path";
        private const string DownloadProgram = "download-program";
        private const string DownloadProgramPath = "download-program-path";
        private const string FeedTimeout = "feed-timeout";
        private const string FeedRetries = "feed-retries";

        public static void Configure(CommandConfigurator configurator)
        {
            string currentPath = ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath ?? "(not set)";
            string currentProgram = ProgramConfiguration.DownloadConfigurations.DownloadProgram ?? "(not set)";
            string currentProgramPath = ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName ?? "(not set)";

            configurator
                .RegisterOption<string>(option => option
                    .Name(DownloadPodcastPath)
                    .Abr("p")
                    .Description($"The downloaded podcast saved path. Current: {currentPath}."))
                .RegisterOption<string>(option => option
                    .Name(DownloadProgram)
                    .Abr("dp")
                    .Description($"The downloaded program name ({DownloadTools.Aria2Name} or {DownloadTools.IdmName}). Current: {currentProgram}."))
                .RegisterOption<string>(option => option
                    .Name(DownloadProgramPath)
                    .Abr("dpp")
                    .Description($"The downloaded program path. Current: {currentProgramPath}."))
                .RegisterOption<string>(option => option
                    .Name(FeedTimeout)
                    .Abr("ft")
                    .Description($"The timeout of one feed download attempt, in seconds ({DownloadConfiguration.MinFeedRequestTimeoutSeconds}-{DownloadConfiguration.MaxFeedRequestTimeoutSeconds}). Current: {ProgramConfiguration.DownloadConfigurations.FeedRequestTimeoutSeconds}."))
                .RegisterOption<string>(option => option
                    .Name(FeedRetries)
                    .Abr("fr")
                    .Description($"The maximum number of feed download attempts, including the first ({DownloadConfiguration.MinFeedDownloadMaxAttempts}-{DownloadConfiguration.MaxFeedDownloadMaxAttempts}). Current: {ProgramConfiguration.DownloadConfigurations.FeedDownloadMaxAttempts}."))
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            Logger.Log.Info("Enter Config command.");

            string downloadPodcastPath = commandArgs.GetOption<string>(DownloadPodcastPath);
            string downloadProgram = commandArgs.GetOption<string>(DownloadProgram);
            string downloadProgramPath = commandArgs.GetOption<string>(DownloadProgramPath);
            string feedTimeoutValue = commandArgs.GetOption<string>(FeedTimeout);
            string feedRetriesValue = commandArgs.GetOption<string>(FeedRetries);

            Logger.Log.Info($"Input DownloadPodcastPath config to {downloadPodcastPath ?? "(not provided)"}.");
            Logger.Log.Info($"Input DownloadProgram config to {downloadProgram ?? "(not provided)"}.");
            Logger.Log.Info($"Input DownloadProgramPath config to {downloadProgramPath ?? "(not provided)"}.");
            Logger.Log.Info($"Input FeedTimeout config to {feedTimeoutValue ?? "(not provided)"}.");
            Logger.Log.Info($"Input FeedRetries config to {feedRetriesValue ?? "(not provided)"}.");

            bool isValid = true;

            if (downloadPodcastPath != null)
            {
                if (Directory.Exists(downloadPodcastPath))
                {
                    ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath = downloadPodcastPath;
                }
                else
                {
                    Logger.Log.Warn("DownloadPodcastPath is NOT valid.");

                    output.WriteError("Error. Input of 'download-path' does not exist.");
                    isValid = false;
                }
            }

            if (downloadProgram != null)
            {
                if (downloadProgram == DownloadTools.IdmName || downloadProgram == DownloadTools.Aria2Name)
                {
                    ProgramConfiguration.DownloadConfigurations.DownloadProgram = downloadProgram;
                }
                else
                {
                    Logger.Log.Warn("DownloadProgram is NOT valid.");

                    output.WriteError($"Error. Input of 'download-program' is illegal. Please input {DownloadTools.Aria2Name} or {DownloadTools.IdmName}.");
                    isValid = false;
                }
            }

            if (downloadProgramPath != null)
            {
                if (!TrySetDownloadProgramPath(downloadProgramPath, output))
                {
                    isValid = false;
                }
            }

            if (feedTimeoutValue != null)
            {
                if (TryParseInRange(feedTimeoutValue, DownloadConfiguration.MinFeedRequestTimeoutSeconds,
                        DownloadConfiguration.MaxFeedRequestTimeoutSeconds, out int feedTimeout))
                {
                    ProgramConfiguration.DownloadConfigurations.FeedRequestTimeoutSeconds = feedTimeout;
                }
                else
                {
                    Logger.Log.Warn("FeedTimeout is NOT valid.");

                    output.WriteError(
                        $"Error. Input of 'feed-timeout' must be an integer from {DownloadConfiguration.MinFeedRequestTimeoutSeconds} to {DownloadConfiguration.MaxFeedRequestTimeoutSeconds}.");
                    isValid = false;
                }
            }

            if (feedRetriesValue != null)
            {
                if (TryParseInRange(feedRetriesValue, DownloadConfiguration.MinFeedDownloadMaxAttempts,
                        DownloadConfiguration.MaxFeedDownloadMaxAttempts, out int feedRetries))
                {
                    ProgramConfiguration.DownloadConfigurations.FeedDownloadMaxAttempts = feedRetries;
                }
                else
                {
                    Logger.Log.Warn("FeedRetries is NOT valid.");

                    output.WriteError(
                        $"Error. Input of 'feed-retries' must be an integer from {DownloadConfiguration.MinFeedDownloadMaxAttempts} to {DownloadConfiguration.MaxFeedDownloadMaxAttempts}.");
                    isValid = false;
                }
            }

            ProgramConfiguration.SaveConfig();

            Logger.Log.Info("Finish Config command.");

            if (isValid)
            {
                output.WriteLine("Done.");
            }
            else
            {
                output.WriteError("Done with errors.");
            }

            return isValid ? ExitCode.Success : ExitCode.Failure;
        }

        private static bool TrySetDownloadProgramPath(string downloadProgramPath, IOutput output)
        {
            if (string.IsNullOrEmpty(downloadProgramPath))
            {
                Logger.Log.Warn("DownloadProgramPath is empty.");

                output.WriteError("Error. Input of 'download-program-path' is empty.");
                return false;
            }

            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT
                && !downloadProgramPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                downloadProgramPath += ".exe";
            }

            if (File.Exists(downloadProgramPath) || ExistsOnPath(downloadProgramPath))
            {
                ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName = downloadProgramPath;
                return true;
            }

            Logger.Log.Warn("DownloadProgramPath is NOT valid.");

            output.WriteError("Error. Input of 'download-program-path' does not exist.");
            return false;
        }

        private static bool TryParseInRange(string value, int min, int max, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                   && result >= min
                   && result <= max;
        }

        private static bool ExistsOnPath(string program)
        {
            string values = System.Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrEmpty(values))
            {
                return false;
            }

            foreach (string path in values.Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string fullPath;

                try
                {
                    fullPath = Path.Combine(path, program);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
