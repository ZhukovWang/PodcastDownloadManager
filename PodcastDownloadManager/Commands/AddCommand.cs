using System;
using System.Collections.Generic;
using System.Text;
using NFlags;
using NFlags.Commands;

namespace PodcastDownloadManager.Commands
{
    public static class AddCommand
    {
        public const string Name = "add";
        public const string Description = "Add a podcast through url.";

        private const string PodcastUrl = "url";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterParameter(PodcastUrl, "The url of podcast.", "Url is not found.")
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            output.WriteLine("Url: {0}", commandArgs.GetParameter<string>(PodcastUrl));
            return 0;
        }
    }
}