using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.IO;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class DownloadCommand
    {
        public const string Name = "download";
        public const string Description = "Download the podcast newly release.";

        private const string Date = "date";
        private const string DownloadDirectory = "dir";
        private const string SimpleFile = "simple-file";

        private static string DownloadDirectoryDefaultPath =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\Download";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterParameter(Date, "Release will be downloaded before the date.", "20200701")
                .RegisterFlag(SimpleFile, "s", "create a simple file just have download url, could use to other download software. And not automatic download.", false)
                .RegisterOption(DownloadDirectory, "d", "download configure and download file direction.", DownloadDirectoryDefaultPath)
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            string podcastsDownloadDirectory = commandArgs.GetOption<string>(DownloadDirectory);
            bool isSimpleFile = commandArgs.GetFlag(SimpleFile);

            output.WriteLine("Building downloading file...");

            if (!Directory.Exists(podcastsDownloadDirectory))
            {
                Directory.CreateDirectory(podcastsDownloadDirectory);
            }

            string url = commandArgs.GetParameter<string>(Date);
            DateTime dt = DateTime.ParseExact(url, "yyyyMMdd", new CultureInfo("en-US"));
            Opml.DownloadPodcastBeforeDate(dt, podcastsDownloadDirectory, isSimpleFile);

            output.WriteLine("Building done");

            if (!isSimpleFile)
            {
                output.WriteLine("Staring aria2 to download...");

                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "aria2c",
                        Arguments = $"-i \"{Opml.DownloadFileName}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.EnableRaisingEvents = true;

                process.Exited += new EventHandler(((sender, args) => output.WriteLine("Done.")));
                process.OutputDataReceived += new DataReceivedEventHandler(((sender, args) => output.WriteLine(args.Data)));
                process.ErrorDataReceived += new DataReceivedEventHandler(((sender, args) => output.WriteLine(args.Data)));

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();
                process.Close();

                output.WriteLine("Done.");
            }
            return 0;
        }
    }
}