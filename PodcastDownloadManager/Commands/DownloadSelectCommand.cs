﻿using System;
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

            if (res != 0)
            {
                Logger.Log.Warn("The input name is NOT in the library.");

                output.WriteLine("Error. Input of Name does not contain in the library.");
                return 0;
            }

            output.WriteLine("Building done");

            Logger.Log.Info("Finish build download file.");

            output.WriteLine("Downloading...");

            if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.Aria2Name)
            {
                Logger.Log.Info("Start download newly release use aria2.");

                DownloadTools.DownloadAria2(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.DownloadFileName, output);
            }
            else if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.IdmName)
            {
                Logger.Log.Info("Start download newly release use idm.");

                DownloadTools.DownloadIdm(ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName, ProgramConfiguration.DownloadFileName, output);
            }

            Logger.Log.Info("Finish download.");

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