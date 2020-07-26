using System;
using System.Collections.Generic;
using System.Text;
using NFlags;
using NFlags.Commands;

namespace PodcastDownloadManager.Commands
{
    public static class UpdateCommand
    {
        public const string Name = "update";
        public const string Description = "Update the podcast newly release.";

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