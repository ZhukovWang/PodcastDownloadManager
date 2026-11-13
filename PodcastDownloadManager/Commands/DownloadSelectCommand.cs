using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NFlags;
using NFlags.Commands;
using NFlags.TypeConverters;
using PodcastDownloadManager.Podcast;

namespace PodcastDownloadManager.Commands
{
    public static class DownloadSelectCommand
    {
        public const string Name = "select";
        public const string Description = "Select need download release.";

        private const string PodcastName = "Name";

        private const string SelectIndex = "Select";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterParameter(PodcastName, "Specific a podcast and show all releases", "None")
                .RegisterParameter<int[]>(b => b
                   .Name(SelectIndex)
                   .Description("select index of podcast release")
                   .Converter(new ArrayInt32Converter()))
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            Logger.Log.Info("Enter Download Select command.");

            int[] selectIndex = commandArgs.GetParameter<int[]>(SelectIndex);
            string name = commandArgs.GetParameter<string>(PodcastName);

            Logger.Log.Info($"Input podcast name is {name}.");
            Logger.Log.Info($"Input select index are {string.Join(";", selectIndex)}.");

            output.WriteLine("Building downloading file...");

            if (!Directory.Exists(ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath))
            {
                Logger.Log.Info($"Create directory, is {ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath}.");

                Directory.CreateDirectory(ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath);
            }

            int res = Opml.DownloadPodcastSelectRelease(name, selectIndex,
                ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath, false,
                ProgramConfiguration.DownloadConfigurations.DownloadProgram);

            if (res == -1)
            {
                Logger.Log.Warn("The input name is NOT in the library.");

                output.WriteError("Error. Input of Name does not contain in the library.");
                return ExitCode.Failure;
            }

            if (res == -2)
            {
                Logger.Log.Warn("The feed of the podcast could not be read.");

                output.WriteError("Error. The feed of this podcast is not available or could not be read. Run 'pdlm update' first.");
                return ExitCode.Failure;
            }

            if (res == -3)
            {
                Logger.Log.Warn("None of the selected releases could be downloaded.");

                output.WriteError("Error. None of the selected indexes matched a downloadable release.");
                return ExitCode.Failure;
            }

            if (res != 0)
            {
                Logger.Log.Warn($"Could not build the download list. Result is {res}.");

                output.WriteError("Error. Could not build the download list.");
                return ExitCode.Failure;
            }

            output.WriteLine("Building done");

            Logger.Log.Info("Finish build download file.");

            output.WriteLine("Downloading...");

            int downloadExitCode = DownloadTools.Download(
                ProgramConfiguration.DownloadConfigurations.DownloadProgram,
                ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName,
                ProgramConfiguration.DownloadFileName, output);

            if (downloadExitCode != ExitCode.Success)
            {
                Logger.Log.Warn($"The downloader exited with code {downloadExitCode}.");
                output.WriteError($"The downloader exited with code {downloadExitCode}.");
                return ExitCode.Failure;
            }

            Logger.Log.Info("Finish download.");

            output.WriteLine("Done.");

            return ExitCode.Success;
        }
    }

    public class ArrayInt32Converter : IArgumentConverter
    {
        public bool CanConvert(Type type)
        {
            return typeof(System.Int32[]) == type;
        }

        public object Convert(Type type, string value)
        {
            var strings = value.Split(";");
            Int32[] values = new int[strings.Length];

            for (int i = 0; i < strings.Length; i++)
            {
                values[i] = System.Convert.ToInt32(strings[i]);
            }

            return values;
        }
    }
}