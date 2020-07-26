using System;
using System.Collections.Generic;
using System.Text;
using NFlags;
using NFlags.Commands;

namespace PodcastDownloadManager.Commands
{
    public static class RemoveCommand
    {
        public const string Name = "remove";
        public const string Description = "Remove a podcast through name.";

        private const string PodcastName = "name";

        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .RegisterParameter(PodcastName, "The name of podcast need to remove.", "Name is not found.")
                .SetExecute(Execute);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            output.WriteLine("Name: {0}", commandArgs.GetParameter<string>(PodcastName));
            return 0;
        }
    }
}