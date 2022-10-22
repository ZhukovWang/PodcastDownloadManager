using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PodcastDownloadManager
{
    public class DownloadConfiguration
    {
        public string DownloadPodcastPath { get; set; }
        public string DownloadProgram { get; set; }
        public string DownloadProgramPathName { get; set; }
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
        private static readonly string Local =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        /// <summary>
        /// config file path
        /// </summary>
        public static readonly string ConfigFilePathName = Local + Path.DirectorySeparatorChar + "config.json";

        /// <summary>
        /// podcast xml saved directory
        /// </summary>
        public static readonly string PodcastsFileDirectory = Local + Path.DirectorySeparatorChar + "PodcastsFile";

        /// <summary>
        /// podcast newly release download info file
        /// </summary>
        public static readonly string PodcastNewlyReleaseInfo = Local + Path.DirectorySeparatorChar + "NewlyReleaseInfo.txt";

        /// <summary>
        /// all podcast info file
        /// </summary>
        public static readonly string PodcastFileName = Local + Path.DirectorySeparatorChar + "podcasts.opml";

        /// <summary>
        /// podcast download info file
        /// </summary>
        public static string DownloadFileName = DownloadConfigurations.DownloadPodcastPath + Path.DirectorySeparatorChar + "PodcastDownload.txt";

        public static void CreateConfig()
        {
            DownloadConfigurations.DownloadPodcastPath = Local + Path.DirectorySeparatorChar + "Download";
            DownloadConfigurations.DownloadProgram = DownloadTools.Aria2Name;
            DownloadConfigurations.DownloadProgramPathName = "aria2c";

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
            DownloadConfigurations = JsonSerializer.Deserialize<DownloadConfiguration>(readOnlySpan, options);
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