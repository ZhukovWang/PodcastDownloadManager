using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
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
        public static DownloadConfiguration DownloadConfigurations = new DownloadConfiguration();

        public static readonly string Local =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public static string ConfigFilePathName = Local + "\\config.json";
        public static string PodcastsFileDirectory = Local + "\\PodcastsFile";

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