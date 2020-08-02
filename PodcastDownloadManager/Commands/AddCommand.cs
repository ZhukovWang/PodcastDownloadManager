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

            Uri uriResult;
            bool result = Uri.TryCreate(url, UriKind.Absolute, out uriResult)
                          && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (result)
            {
                Logger.Log.Info("The Add command input string is a valid url.");

                if (File.Exists(ProgramConfiguration.PodcastFileName))
                {
                    Logger.Log.Info($"PodcastFileName exists. Path is {ProgramConfiguration.PodcastFileName}.");
                    Logger.Log.Info("Add the podcast url.");
                    output.WriteLine("Adding...");
                    string podcastName = Opml.AddPodcast(url);
                    output.WriteLine("Have added a new podcast.");
                    output.WriteLine($"Name: {podcastName}");
                    output.WriteLine($"URL: {url}");
                    Logger.Log.Info($"Url has been added. Name: {podcastName}; URL: {url}.");
                }
                else
                {
                    Logger.Log.Info($"PodcastFileName does NOT exist. Path is {ProgramConfiguration.PodcastFileName}.");
                    Logger.Log.Info("Create the opml file.");
                    output.WriteLine("Not find old profile, creating a new one...");
                    Opml.Create();
                    output.WriteLine("Have created a new profile.");
                    Logger.Log.Info("The opml file has benn created.");
                    Logger.Log.Info("Add the podcast url.");
                    string podcastName = Opml.AddPodcast(url);
                    output.WriteLine("Have added a new podcast.");
                    output.WriteLine($"Name: {podcastName}");
                    output.WriteLine($"URL: {url}");
                    Logger.Log.Info($"Url has been added. Name: {podcastName}; URL: {url}.");
                }
            }
            else
            {
                Logger.Log.Warn("The Add command input string is NOT a valid url.");

                output.WriteLine("URL is not valid.");
            }

            return 0;
        }
    }
}