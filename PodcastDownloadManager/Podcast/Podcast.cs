using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace PodcastDownloadManager.Podcast
{
    public class Podcast
    {
        public string Url;

        public string Name;

        private string FileName;

        public Podcast(string url)
        {
            this.Url = url;
            FileName = "PodcastsFile/NewPodcast.xml";
            Download(url);
            this.Name = GetPodcastName();
        }

        public Podcast(string url, string name)
        {
            this.Url = url;
            this.Name = name;
            FileName = $"PodcastsFile/{name}.xml";
            Download(url);
        }

        private void Download(string url)
        {
            if (File.Exists(FileName))
            {
                File.Delete(FileName);
            }

            using var client = new WebClient();
            client.DownloadFile(url, FileName);
        }

        private string GetPodcastName()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(FileName);

            var root = xmlDoc.DocumentElement;

            XmlNode channel = root.SelectSingleNode("channel");

            XmlNode titleNode = channel.SelectSingleNode("title");

            return titleNode.InnerText.Trim().Replace(" ", "");
        }

        public string GetPodcastDetail()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(FileName);

            var root = xmlDoc.DocumentElement;

            XmlNode channel = root.SelectSingleNode("channel");

            string title = channel.SelectSingleNode("title").InnerText;

            string description = RemoveStripHtml(channel.SelectSingleNode("description").InnerText);

            XmlNode newlyRelease = channel.SelectSingleNode("item");

            string newlyReleaseTitle = newlyRelease.SelectSingleNode("title").InnerText;
            string newlyReleaseDescription = RemoveStripHtml(newlyRelease.SelectSingleNode("description").InnerText);
            string newlyReleasePubDate = newlyRelease.SelectSingleNode("pubDate").InnerText;
            DateTime dt = DateTime.ParseExact(newlyReleasePubDate, "ddd, dd MMM yyyy HH:mm:ss zzz", new CultureInfo("en-us"));
            newlyReleasePubDate = dt.ToString("G");
            string newlyReleaseDownloadUrl = newlyRelease.SelectSingleNode("enclosure").Attributes["url"].Value;

            return $"Title: {title}\n" +
                   $"Description: {description}\n\n" +
                   "Newly release:\n" +
                   $"Title: {newlyReleaseTitle}\n" +
                   $"Description: {newlyReleaseDescription}\n" +
                   $"Public date: {newlyReleasePubDate}\n" +
                   $"Download url: {newlyReleaseDownloadUrl}";
        }

        private string RemoveStripHtml(string strHtml)
        {
            string stringOutput = strHtml;
            Regex regex = new Regex(@"<[^>]+>|</[^>]+>");
            stringOutput = regex.Replace(stringOutput, "");
            return stringOutput;
        }
    }
}