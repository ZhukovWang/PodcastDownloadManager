﻿using System;
using System.Collections.Generic;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.FileMetadata;

namespace PodcastDownloadManager.Commands
{
    public static class AutoMetadataCommand
    {
        public const string Name = "autometadata";
        public const string Description = "Auto add metadata to podcast file.";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            Logger.Log.Info("Enter AutoMetadata command.");

            AudioMetadata.AutoAddMetadata(out var outputList);

            Logger.Log.Info("Finish AutoMetadata.");

            foreach (string s in outputList)
            {
                output.WriteLine(s);
            }
            output.WriteLine("Done.");
            return 0;
        }
    }
}