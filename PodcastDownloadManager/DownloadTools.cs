using NFlags;
using System.Diagnostics;
using System.IO;

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

            process.Exited += ((sender, args) => output.WriteLine("Done."));
            process.OutputDataReceived += ((sender, args) => output.WriteLine(args.Data));
            process.ErrorDataReceived += ((sender, args) => output.WriteLine(args.Data));

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

                processAddDownload.OutputDataReceived += ((sender, args) => output.WriteLine(args.Data));
                processAddDownload.ErrorDataReceived += ((sender, args) => output.WriteLine(args.Data));

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

            processStartDownload.OutputDataReceived += ((sender, args) => output.WriteLine(args.Data));
            processStartDownload.ErrorDataReceived += ((sender, args) => output.WriteLine(args.Data));

            processStartDownload.Start();

            processStartDownload.BeginOutputReadLine();
            processStartDownload.BeginErrorReadLine();

            processStartDownload.WaitForExit();
            processStartDownload.Close();
        }
    }
}