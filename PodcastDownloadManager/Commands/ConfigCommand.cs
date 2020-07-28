using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class ConfigCommand
    {
        public const string Name = "config";
        public const string Description = "Set configuration values.";

        private const string DownloadPodcastPath = "download-path";
        private const string DownloadProgram = "download-program";
        private const string DownloadProgramPath = "download-program-path";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterOption(DownloadPodcastPath, "p", "The downloaded podcast saved path.", ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath)
                .RegisterOption(DownloadProgram, "dp", $"The downloaded program name, now support are {DownloadTools.Aria2Name} and {DownloadTools.IdmName}.", ProgramConfiguration.DownloadConfigurations.DownloadProgram)
                .RegisterOption(DownloadProgramPath, "dpp", $"The downloaded program path.", ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName)
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            string downloadPodcastPath = commandArgs.GetOption<string>(DownloadPodcastPath);
            string downloadProgram = commandArgs.GetOption<string>(DownloadProgram);
            string downloadProgramPath = commandArgs.GetOption<string>(DownloadProgramPath);

            if (Directory.Exists(downloadPodcastPath))
            {
                ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath = downloadPodcastPath;
            }
            else
            {
                output.WriteLine("Error. Input of 'download-path' does not exist.");
            }

            if (downloadProgram == DownloadTools.IdmName || downloadProgram == DownloadTools.Aria2Name)
            {
                ProgramConfiguration.DownloadConfigurations.DownloadProgram = downloadProgram;
            }
            else
            {
                output.WriteLine($"Error. Input of 'download-program' is illegal. Please input {DownloadTools.Aria2Name} or {DownloadTools.IdmName}.");
            }

            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (!downloadProgramPath.Contains(".exe"))
                {
                    downloadProgramPath += ".exe";
                }
            }

            if (File.Exists(downloadProgramPath))
            {
                ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName = downloadProgramPath;
            }
            else
            {
                bool isValid = false;
                var values = System.Environment.GetEnvironmentVariable("PATH");
                foreach (var path in values.Split(Path.PathSeparator))
                {
                    var fullPath = Path.Combine(path, downloadProgramPath);
                    if (File.Exists(fullPath))
                    {
                        isValid = true;
                        break;
                    }
                }

                if (isValid)
                {
                    ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName = downloadProgramPath;
                }
                else
                {
                    output.WriteLine("Error. Input of 'download-program-path' does not exist.");
                }
            }

            ProgramConfiguration.SaveConfig();

            output.WriteLine("Done.");

            return 0;
        }
    }
}