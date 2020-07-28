using System;
using System.Collections.Generic;
using System.IO;
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

        private const string SelectIndex = "select";

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
            int[] selectIndexs = commandArgs.GetParameter<int[]>(SelectIndex);
            string name = commandArgs.GetParameter<string>(PodcastName);

            output.WriteLine("Building downloading file...");

            if (!Directory.Exists(ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath))
            {
                Directory.CreateDirectory(ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath);
            }

            Opml.DownloadPodcastSelectRelease(name, selectIndexs,
                ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath, false,
                ProgramConfiguration.DownloadConfigurations.DownloadProgram);

            output.WriteLine("Building done");

            output.WriteLine("Downloading...");

            if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.Aria2Name)
            {
                DownloadTools.DownloadAria2(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.DownloadFileName, output);
            }
            else if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.IdmName)
            {
                DownloadTools.DownloadIdm(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.DownloadFileName, output);
            }

            output.WriteLine("Done.");

            return 0;
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