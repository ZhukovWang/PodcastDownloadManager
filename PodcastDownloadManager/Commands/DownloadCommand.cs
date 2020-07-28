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

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterParameter(Date, "Release will be downloaded after the date.", "20200701")
                .RegisterFlag(SimpleFile, "s", "create a simple file just have download url, could use to other download software. And not automatic download.", false)
                .RegisterOption(DownloadDirectory, "d", "download file directory.", ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath)
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
            Opml.DownloadPodcastAfterDate(dt, podcastsDownloadDirectory, isSimpleFile, ProgramConfiguration.DownloadConfigurations.DownloadProgram);

            output.WriteLine("Building done");

            if (!isSimpleFile)
            {
                output.WriteLine("Downloading...");

                if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == ProgramConfiguration.Aria2Name)
                {
                    DownloadAria2(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, output);
                }
                else if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == ProgramConfiguration.IdmName)
                {
                    DownloadIdm(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, output);
                }

                output.WriteLine("Done.");
            }
            return 0;
        }

        private static void DownloadAria2(string programPath, IOutput output)
        {
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = programPath,
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
        }

        private static void DownloadIdm(string programPath, IOutput output)
        {
            string[] downloadCommandStrings = File.ReadAllLines(Opml.DownloadFileName);

            foreach (string commandString in downloadCommandStrings)
            {
                var processAddDownload = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = programPath,
                        Arguments = $"{commandString}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                processAddDownload.EnableRaisingEvents = true;

                processAddDownload.OutputDataReceived += new DataReceivedEventHandler(((sender, args) => output.WriteLine(args.Data)));
                processAddDownload.ErrorDataReceived += new DataReceivedEventHandler(((sender, args) => output.WriteLine(args.Data)));

                processAddDownload.Start();

                processAddDownload.BeginOutputReadLine();
                processAddDownload.BeginErrorReadLine();

                processAddDownload.WaitForExit();
                processAddDownload.Close();
            }

            var processStartDownload = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = programPath,
                    Arguments = "/s",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            processStartDownload.EnableRaisingEvents = true;

            processStartDownload.OutputDataReceived += new DataReceivedEventHandler(((sender, args) => output.WriteLine(args.Data)));
            processStartDownload.ErrorDataReceived += new DataReceivedEventHandler(((sender, args) => output.WriteLine(args.Data)));

            processStartDownload.Start();

            processStartDownload.BeginOutputReadLine();
            processStartDownload.BeginErrorReadLine();

            processStartDownload.WaitForExit();
            processStartDownload.Close();
        }
    }
}