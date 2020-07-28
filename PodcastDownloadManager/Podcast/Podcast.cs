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
            FileName = $"{ProgramConfiguration.PodcastsFileDirectory}/NewPodcast.xml";
            Download(url);
            this.Name = GetPodcastName();
            File.Delete(FileName);
        }

        public Podcast(string name, string url, bool skipDownload = false)
        {
            this.Url = url;
            this.Name = name;
            FileName = $"{ProgramConfiguration.PodcastsFileDirectory}/{name}.xml";
            if (!skipDownload)
            {
                Download(url);
            }
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

            string name = titleNode.InnerText.Trim().Replace(" ", "");

            return GetValidName(name);
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
            DateTime dt = DateTime.ParseExact(newlyReleasePubDate, "ddd, dd MMM yyyy HH:mm:ss K", new CultureInfo("en-us"));
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

        public void GetPodcastNewlyRelease(ref List<string> newlyRelease)
        {
            //Download new xml
            using var client = new WebClient();
            client.DownloadFile(this.Url, $"{FileName}.tmp");

            //get last update time
            DateTime lastUpdateDateTime = DateTime.Now;
            if (File.Exists(FileName))
            {
                lastUpdateDateTime = File.GetLastWriteTimeUtc(FileName);
                File.Delete($"{FileName}");
            }

            File.Copy($"{FileName}.tmp", FileName, true);
            File.Delete($"{FileName}.tmp");

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(FileName);

            var root = xmlDoc.DocumentElement;

            XmlNode channel = root.SelectSingleNode("channel");

            string title = channel.SelectSingleNode("title").InnerText;

            XmlNodeList nodeList = channel.ChildNodes;

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].Name == "item")
                {
                    string newlyReleasePubDate = nodeList[i].SelectSingleNode("pubDate").InnerText;
                    DateTime dt = DateTime.Parse(newlyReleasePubDate);
                    if (dt > lastUpdateDateTime)
                    {
                        string newlyReleaseTitle =
                            nodeList[i].SelectSingleNode("title").InnerText.Trim();

                        newlyReleasePubDate = dt.ToString("yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo);

                        string showString = $"* {this.Name} - {newlyReleaseTitle} - {newlyReleasePubDate}";

                        newlyRelease.Add(showString);

                        newlyReleasePubDate = dt.ToString("yyyy_MM_dd", DateTimeFormatInfo.InvariantInfo);

                        FileStream fs = File.Open(ProgramConfiguration.PodcastNewlyReleaseInfo, FileMode.CreateNew);

                        newlyReleaseTitle =
                            nodeList[i].SelectSingleNode("title").InnerText.Trim();
                        string newlyReleaseDownloadUrl =
                            nodeList[i].SelectSingleNode("enclosure").Attributes["url"].Value;

                        string fileName = GetValidName($"{title} - {newlyReleaseTitle} - {newlyReleasePubDate}.mp3");
                        if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == ProgramConfiguration.Aria2Name)
                        {
                            AddText(fs, $"{newlyReleaseDownloadUrl}\n");
                            AddText(fs, $"\tdir={ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath}\n");
                            AddText(fs, $"\tout={fileName}\n");
                        }
                        else if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == ProgramConfiguration.IdmName)
                        {
                            AddText(fs, $"/a /d \"{newlyReleaseDownloadUrl}\" /p \"{ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath}\" /f \"{fileName}\"\n");
                        }
                    }
                }
            }
        }

        public void BuildPodcastDownloadFile(DateTime dateTime, string downloadDirectory, bool isSimpleFile, string downloadProgram, ref FileStream fs)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(FileName);

            var root = xmlDoc.DocumentElement;

            XmlNode channel = root.SelectSingleNode("channel");

            string title = channel.SelectSingleNode("title").InnerText;

            XmlNodeList nodeList = channel.ChildNodes;

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].Name == "item")
                {
                    string newlyReleasePubDate = nodeList[i].SelectSingleNode("pubDate").InnerText;
                    DateTime dt = DateTime.Now;
                    dt = DateTime.Parse(newlyReleasePubDate);

                    if (dt > dateTime)
                    {
                        newlyReleasePubDate = dt.ToString("yyyy_MM_dd", DateTimeFormatInfo.InvariantInfo);

                        string newlyReleaseTitle =
                            nodeList[i].SelectSingleNode("title").InnerText.Trim();
                        string newlyReleaseDownloadUrl =
                            nodeList[i].SelectSingleNode("enclosure").Attributes["url"].Value;

                        if (isSimpleFile)
                        {
                            AddText(fs, $"{newlyReleaseDownloadUrl}\n");
                        }
                        else
                        {
                            string fileName = GetValidName($"{title} - {newlyReleaseTitle} - {newlyReleasePubDate}.mp3");
                            if (downloadProgram == ProgramConfiguration.Aria2Name)
                            {
                                AddText(fs, $"{newlyReleaseDownloadUrl}\n");
                                AddText(fs, $"\tdir={downloadDirectory}\n");
                                AddText(fs, $"\tout={fileName}\n");
                            }
                            else if (downloadProgram == ProgramConfiguration.IdmName)
                            {
                                AddText(fs, $"/a /d \"{newlyReleaseDownloadUrl}\" /p \"{downloadDirectory}\" /f \"{fileName}\"\n");
                            }
                        }
                    }
                }
            }
        }

        private string RemoveStripHtml(string strHtml)
        {
            string stringOutput = strHtml;
            Regex regex = new Regex(@"<[^>]+>|</[^>]+>");
            stringOutput = regex.Replace(stringOutput, "");
            return stringOutput;
        }

        private static void AddText(FileStream fs, string value)
        {
            byte[] info = new UTF8Encoding(true).GetBytes(value);
            fs.Write(info, 0, info.Length);
        }

        private static string GetValidName(string name)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(name, "");
        }
    }
}