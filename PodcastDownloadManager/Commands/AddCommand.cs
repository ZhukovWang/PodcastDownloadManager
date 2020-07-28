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
            string url = commandArgs.GetParameter<string>(PodcastUrl);

            Uri uriResult;
            bool result = Uri.TryCreate(url, UriKind.Absolute, out uriResult)
                          && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (result)
            {
                if (File.Exists(ProgramConfiguration.PodcastFileName))
                {
                    output.WriteLine("Adding...");
                    string podcastName = Opml.AddPodcast(url);
                    output.WriteLine("Have added a new podcast.");
                    output.WriteLine($"Name: {podcastName}");
                    output.WriteLine($"URL: {url}");
                }
                else
                {
                    output.WriteLine("Not find old profile, creating a new one...");
                    Opml.Create();
                    output.WriteLine("Have created a new profile.");
                    string podcastName = Opml.AddPodcast(url);
                    output.WriteLine("Have added a new podcast.");
                    output.WriteLine($"Name: {podcastName}");
                    output.WriteLine($"URL: {url}");
                }
            }
            else
            {
                output.WriteLine("URL is not valid.");
            }

            return 0;
        }
    }
}