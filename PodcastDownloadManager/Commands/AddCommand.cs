using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NFlags;
using NFlags.Commands;
using PodcastDownloadManager.Podcast;

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
            Logger.Log.Info("Enter Add command.");

            string url = commandArgs.GetParameter<string>(PodcastUrl);

            Logger.Log.Info($"Get the Add command input string, is {url}.");

            bool result = Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                          && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (!result)
            {
                Logger.Log.Warn("The Add command input string is NOT a valid url.");

                output.WriteError("URL is not valid.");
                return ExitCode.Failure;
            }

            Logger.Log.Info("The Add command input string is a valid url.");

            if (!File.Exists(ProgramConfiguration.PodcastFileName))
            {
                Logger.Log.Info($"PodcastFileName does NOT exist. Path is {ProgramConfiguration.PodcastFileName}.");
                Logger.Log.Info("Create the opml file.");
                output.WriteLine("Not find old profile, creating a new one...");
                Opml.Create();
                output.WriteLine("Have created a new profile.");
                Logger.Log.Info("The opml file has benn created.");
            }

            Logger.Log.Info("Add the podcast url.");
            output.WriteLine("Adding...");

            string podcastName = Opml.AddPodcast(url);

            if (string.IsNullOrEmpty(podcastName))
            {
                Logger.Log.Warn($"Url has NOT been added, the feed could not be read. URL: {url}.");

                output.WriteError("Could not read the podcast feed. Please check the URL and try again.");
                return ExitCode.Failure;
            }

            output.WriteLine("Have added a new podcast.");
            output.WriteLine($"Name: {podcastName}");
            output.WriteLine($"URL: {url}");
            Logger.Log.Info($"Url has been added. Name: {podcastName}; URL: {url}.");

            return ExitCode.Success;
        }
    }
}