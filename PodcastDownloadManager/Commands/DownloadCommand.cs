using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class DownloadCommand
    {
        public const string Name = "download";
        public const string Description = "Download the podcast newly release.";

        private const string Date = "20200726";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterParameter(Date, "Release will be downloaded before the date.", "Date is wrong.")
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            output.WriteLine("Building downloading file...");

            string url = commandArgs.GetParameter<string>(Date);
            DateTime dt = DateTime.ParseExact(url, "yyyyMMdd", new CultureInfo("en-US"));
            Opml.DownloadPodcastBeforeDate(dt);

            output.WriteLine("Building done. Staring aria2 to download...");

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

            return 0;
        }
    }
}