using System;
using System.Collections.Generic;
using System.Text;
using NFlags;
using NFlags.Commands;

namespace PodcastDownloadManager.Commands
{
    public static class DownloadCommand
    {
        public const string Name = "download";
        public const string Description = "Download the podcast newly release.";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            return 0;
        }
    }
}