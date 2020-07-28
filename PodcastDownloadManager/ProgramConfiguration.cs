using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

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
        public static readonly string Local =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        /// <summary>
        /// config file path
        /// </summary>
        public static string ConfigFilePathName = Local + "\\config.json";

        /// <summary>
        /// podcast xml saved directory
        /// </summary>
        public static string PodcastsFileDirectory = Local + "\\PodcastsFile";

        /// <summary>
        /// podcast newly release download info file
        /// </summary>
        public static readonly string PodcastNewlyReleaseInfo = Local + "\\NewlyReleaseInfo.txt";

        /// <summary>
        /// all podcast info file
        /// </summary>
        public static string PodcastFileName = Local + "\\podcasts.opml";

        /// <summary>
        /// podcast download info file
        /// </summary>
        public static string DownloadFileName = DownloadConfigurations.DownloadPodcastPath + "\\PodcastDownload.txt";

        public const string Aria2Name = "Aria2";
        public const string IdmName = "IDM";

        public static void CreateConfig()
        {
            DownloadConfigurations.DownloadPodcastPath = Local + "\\Download";
            DownloadConfigurations.DownloadProgram = ProgramConfiguration.Aria2Name;
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
                IgnoreNullValues = true
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