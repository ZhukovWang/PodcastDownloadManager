using System;
using System.Collections.Generic;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Commands;

namespace PodcastDownloadManager
{
    public static class RootCommand
    {
        public static void Configure(CommandConfigurator configurator)
        {
            configurator
                .PrintHelpOnExecute()
                .SetExecute(Execute)
                .RegisterCommand(AddCommand.Name, AddCommand.Description, AddCommand.Configure)
                .RegisterCommand(RemoveCommand.Name, RemoveCommand.Description, RemoveCommand.Configure)
                .RegisterCommand(ShowCommand.Name, ShowCommand.Description, ShowCommand.Configure)
                .RegisterCommand(ListCommand.Name, ListCommand.Description, ListCommand.Configure)
                .RegisterCommand(UpdateCommand.Name, UpdateCommand.Description, UpdateCommand.Configure)
                .RegisterCommand(DownloadCommand.Name, DownloadCommand.Description, DownloadCommand.Configure)
                .RegisterCommand(ConfigCommand.Name, ConfigCommand.Description, ConfigCommand.Configure);
        }

        private static int Execute(CommandArgs commandArgs, IOutput output)
        {
            return 0;
        }
    }
}