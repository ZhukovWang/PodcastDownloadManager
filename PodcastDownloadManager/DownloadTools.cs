using NFlags;
using System;
using System.Diagnostics;
using System.IO;

namespace PodcastDownloadManager
{
    public static class DownloadTools
    {
        public const string Aria2Name = "Aria2";
        public const string IdmName = "IDM";

        /// <summary>
        /// Starts the configured downloader with <paramref name="downloadInfoFile"/> and returns its
        /// exit code. Starting the process is reported as a failure instead of an exception.
        /// </summary>
        public static int Download(string downloadProgram, string programPath, string downloadInfoFile, IOutput output)
        {
            try
            {
                if (downloadProgram == Aria2Name)
                {
                    return DownloadAria2(programPath, downloadInfoFile, output);
                }

                if (downloadProgram == IdmName)
                {
                    return DownloadIdm(programPath, downloadInfoFile, output);
                }

                Logger.Log.Error($"Unknown download program \"{downloadProgram}\".");
                output.WriteError($"Unknown download program \"{downloadProgram}\". Use \"{Aria2Name}\" or \"{IdmName}\".");
                return ExitCode.Failure;
            }
            catch (Exception e)
            {
                Logger.Log.Error($"Could not run the downloader \"{programPath}\". Exception is {e}");
                output.WriteError($"Could not run the downloader \"{programPath}\": {e.Message}");
                return ExitCode.Failure;
            }
        }

        public static int DownloadAria2(string programPath, string downloadInfoFile, IOutput output)
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
            process.ErrorDataReceived += ((sender, args) => output.WriteError(args.Data));

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            int exitCode = process.ExitCode;
            process.Close();

            return exitCode;
        }

        public static int DownloadIdm(string programPath, string downloadInfoFile, IOutput output)
        {
            int exitCode = 0;

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
                processAddDownload.ErrorDataReceived += ((sender, args) => output.WriteError(args.Data));

                processAddDownload.Start();

                processAddDownload.BeginOutputReadLine();
                processAddDownload.BeginErrorReadLine();

                processAddDownload.WaitForExit();

                if (processAddDownload.ExitCode != 0 && exitCode == 0)
                {
                    exitCode = processAddDownload.ExitCode;
                }

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
            processStartDownload.ErrorDataReceived += ((sender, args) => output.WriteError(args.Data));

            processStartDownload.Start();

            processStartDownload.BeginOutputReadLine();
            processStartDownload.BeginErrorReadLine();

            processStartDownload.WaitForExit();

            if (processStartDownload.ExitCode != 0 && exitCode == 0)
            {
                exitCode = processStartDownload.ExitCode;
            }

            processStartDownload.Close();

            return exitCode;
        }
    }
}
