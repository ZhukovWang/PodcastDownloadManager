using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PodcastDownloadManager
{
    public class DownloadConfiguration
    {
        public const int DefaultFeedRequestTimeoutSeconds = 30;

        public const int DefaultFeedDownloadMaxAttempts = 3;

        public const int MinFeedRequestTimeoutSeconds = 1;

        public const int MaxFeedRequestTimeoutSeconds = 600;

        public const int MinFeedDownloadMaxAttempts = 1;

        public const int MaxFeedDownloadMaxAttempts = 10;

        public string DownloadPodcastPath { get; set; }
        public string DownloadProgram { get; set; }
        public string DownloadProgramPathName { get; set; }

        /// <summary>
        /// How long a single feed download attempt may take, headers and body included.
        /// </summary>
        public int FeedRequestTimeoutSeconds { get; set; } = DefaultFeedRequestTimeoutSeconds;

        /// <summary>
        /// How many times a transiently failing feed download is attempted, in total.
        /// </summary>
        public int FeedDownloadMaxAttempts { get; set; } = DefaultFeedDownloadMaxAttempts;
    }

    internal static class ProgramConfiguration
    {
        /// <summary>
        /// pdlm configurations
        /// </summary>
        public static DownloadConfiguration DownloadConfigurations = new DownloadConfiguration();

        /// <summary>
        /// pdlm location
        /// </summary>
        private static string _local = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;

        /// <summary>
        /// config file path
        /// </summary>
        public static string ConfigFilePathName { get; private set; }

        /// <summary>
        /// podcast xml saved directory
        /// </summary>
        public static string PodcastsFileDirectory { get; private set; }

        /// <summary>
        /// podcast newly release download info file
        /// </summary>
        public static string PodcastNewlyReleaseInfo { get; private set; }

        /// <summary>
        /// all podcast info file
        /// </summary>
        public static string PodcastFileName { get; private set; }

        /// <summary>
        /// podcast download info file
        /// </summary>
        public static string DownloadFileName { get; set; }

        static ProgramConfiguration()
        {
            UseDirectory(_local);
        }

        /// <summary>
        /// pdlm location
        /// </summary>
        public static string Local => _local;

        /// <summary>
        /// Points every path at <paramref name="directory"/>. The tests call this to keep each run in
        /// a temporary directory instead of the build output.
        /// </summary>
        internal static void UseDirectory(string directory)
        {
            _local = directory;

            ConfigFilePathName = Path.Combine(directory, "config.json");
            PodcastsFileDirectory = Path.Combine(directory, "PodcastsFile");
            PodcastNewlyReleaseInfo = Path.Combine(directory, "NewlyReleaseInfo.txt");
            PodcastFileName = Path.Combine(directory, "podcasts.opml");

            DownloadFileName = Path.Combine(DownloadConfigurations.DownloadPodcastPath ?? directory,
                "PodcastDownload.txt");
        }

        public static void CreateConfig()
        {
            DownloadConfigurations.DownloadPodcastPath = Local + Path.DirectorySeparatorChar + "Download";
            DownloadConfigurations.DownloadProgram = DownloadTools.Aria2Name;
            DownloadConfigurations.DownloadProgramPathName = "aria2c";
            DownloadConfigurations.FeedRequestTimeoutSeconds = DownloadConfiguration.DefaultFeedRequestTimeoutSeconds;
            DownloadConfigurations.FeedDownloadMaxAttempts = DownloadConfiguration.DefaultFeedDownloadMaxAttempts;

            // Make the default download directory usable immediately; update writes sidecars there
            // before any downloader runs.
            Directory.CreateDirectory(DownloadConfigurations.DownloadPodcastPath);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(DownloadConfigurations, options);
            File.WriteAllBytes(ConfigFilePathName, jsonUtf8Bytes);
        }

        public static void ReadConfig()
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            byte[] jsonUtf8Bytes = File.ReadAllBytes(ConfigFilePathName);
            var readOnlySpan = new ReadOnlySpan<byte>(jsonUtf8Bytes);
            DownloadConfigurations = JsonSerializer.Deserialize<DownloadConfiguration>(readOnlySpan, options)
                                     ?? new DownloadConfiguration();
        }

        public static void SaveConfig()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(DownloadConfigurations, options);
            File.WriteAllBytes(ConfigFilePathName, jsonUtf8Bytes);
        }
    }
}