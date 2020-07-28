using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NFlags;

namespace PodcastDownloadManager
{
    public static class DownloadTools
    {
        public const string Aria2Name = "Aria2";
        public const string IdmName = "IDM";

        public static void DownloadAria2(string programPath, string downloadInfoFile, IOutput output)
        {
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = programPath,
                    Arguments = $"-i \"{downloadInfoFile}\"",
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

        public static void DownloadIdm(string programPath, string downloadInfoFile, IOutput output)
        {
            string[] downloadCommandStrings = File.ReadAllLines(downloadInfoFile);

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