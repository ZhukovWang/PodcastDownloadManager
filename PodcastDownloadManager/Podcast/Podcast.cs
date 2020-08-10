using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using PodcastDownloadManager.FileMetadata;

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
            FileName = $"{ProgramConfiguration.PodcastsFileDirectory}{Path.DirectorySeparatorChar}NewPodcast.xml";
            Download(url);
            this.Name = GetPodcastName();
            File.Delete(FileName);
        }

        public Podcast(string name, string url, bool skipDownload = false)
        {
            this.Url = url;
            this.Name = name;
            FileName = $"{ProgramConfiguration.PodcastsFileDirectory}{Path.DirectorySeparatorChar}{name}.xml";
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

            Logger.Log.Info($"Start download podcast xml file. Url is {url}.");
            using var client = new WebClient();
            client.DownloadFile(url, FileName);
            Logger.Log.Info($"Download finish. File path is {FileName}.");
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
            DateTime dt = DateTime.Parse(newlyReleasePubDate).ToUniversalTime();
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
            Logger.Log.Info($"Get the last update time is {lastUpdateDateTime.ToString("G")}.");

            File.Copy($"{FileName}.tmp", FileName, true);
            File.Delete($"{FileName}.tmp");

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(FileName);

            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd");

            var root = xmlDoc.DocumentElement;

            XmlNode channel = root.SelectSingleNode("channel");

            string title = channel.SelectSingleNode("title").InnerText;

            XmlNodeList nodeList = channel.ChildNodes;

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].Name == "item")
                {
                    string newlyReleasePubDate = nodeList[i].SelectSingleNode("pubDate").InnerText;
                    DateTime dt = DateTime.Parse(newlyReleasePubDate).ToUniversalTime();
                    if (dt > lastUpdateDateTime)
                    {
                        string newlyReleaseTitle =
                            nodeList[i].SelectSingleNode("title").InnerText.Trim();

                        Logger.Log.Info($"Get the newly release title is {newlyReleaseTitle}, time is {dt.ToString("G")}.");

                        newlyReleasePubDate = dt.ToString("yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo);

                        string showString = $"* {this.Name} - {newlyReleaseTitle} - {newlyReleasePubDate}";

                        newlyRelease.Add(showString);

                        newlyReleasePubDate = dt.ToString("yyyy_MM_dd", DateTimeFormatInfo.InvariantInfo);

                        string newlyReleaseDownloadUrl =
                            nodeList[i].SelectSingleNode("enclosure").Attributes["url"].Value;

                        string fileExtension = ".mp3";
                        if (newlyReleaseDownloadUrl.Contains(".m4a"))
                        {
                            fileExtension = ".m4a";
                        }

                        string fileName = GetValidName($"{title} - {newlyReleaseTitle} - {newlyReleasePubDate}{fileExtension}");

                        FileStream fs = File.Open(ProgramConfiguration.PodcastNewlyReleaseInfo, FileMode.Append);

                        if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.Aria2Name)
                        {
                            FileTools.AddText(fs, $"{newlyReleaseDownloadUrl}\n");
                            FileTools.AddText(fs, $"\tdir={ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath}\n");
                            FileTools.AddText(fs, $"\tout={fileName}\n");
                        }
                        else if (ProgramConfiguration.DownloadConfigurations.DownloadProgram == DownloadTools.IdmName)
                        {
                            FileTools.AddText(fs, $"/a /d \"{newlyReleaseDownloadUrl}\" /p \"{ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath}\" /f \"{fileName}\"\n");
                        }

                        fs.Close();

                        string author = title;
                        if (nodeList[i].SelectSingleNode("author") != null)
                        {
                            author = nodeList[i].SelectSingleNode("author").InnerText;
                        }
                        else if (nodeList[i].SelectSingleNode("itunes:author", nsmgr) != null)
                        {
                            author = nodeList[i].SelectSingleNode("itunes:author", nsmgr).InnerText;
                        }

                        string imageUrl = nodeList[i].SelectSingleNode("itunes:image", nsmgr).Attributes["href"].Value;

                        string description = RemoveStripHtml(nodeList[i].SelectSingleNode("description").InnerText);

                        string metadataFilePath = ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath + Path.DirectorySeparatorChar +
                                                  fileName + ".metadata";
                        AudioMetadata.CreateAudioMetadata(metadataFilePath, title, newlyReleaseTitle, author, imageUrl, description,
                            dt);
                    }
                }
            }
        }

        public void BuildPodcastDownloadFile(DateTime dateTime, string downloadDirectory, bool isSimpleFile, string downloadProgram, ref FileStream fs)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(FileName);

            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd");

            var root = xmlDoc.DocumentElement;

            XmlNode channel = root.SelectSingleNode("channel");

            string title = channel.SelectSingleNode("title").InnerText;

            XmlNodeList nodeList = channel.ChildNodes;

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].Name == "item")
                {
                    string newlyReleasePubDate = nodeList[i].SelectSingleNode("pubDate").InnerText;
                    DateTime dt = DateTime.Now.ToUniversalTime();
                    dt = DateTime.Parse(newlyReleasePubDate).ToUniversalTime();

                    if (dt > dateTime)
                    {
                        newlyReleasePubDate = dt.ToString("yyyy_MM_dd", DateTimeFormatInfo.InvariantInfo);

                        string newlyReleaseTitle =
                            nodeList[i].SelectSingleNode("title").InnerText.Trim();
                        string newlyReleaseDownloadUrl =
                            nodeList[i].SelectSingleNode("enclosure").Attributes["url"].Value;

                        Logger.Log.Info($"Get the newly release title is {newlyReleaseTitle}, time is {dt.ToString("G")}.");

                        if (isSimpleFile)
                        {
                            FileTools.AddText(fs, $"{newlyReleaseDownloadUrl}\n");
                        }
                        else
                        {
                            string fileExtension = ".mp3";
                            if (newlyReleaseDownloadUrl.Contains(".m4a"))
                            {
                                fileExtension = ".m4a";
                            }

                            string fileName = GetValidName($"{title} - {newlyReleaseTitle} - {newlyReleasePubDate}{fileExtension}");
                            if (downloadProgram == DownloadTools.Aria2Name)
                            {
                                FileTools.AddText(fs, $"{newlyReleaseDownloadUrl}\n");
                                FileTools.AddText(fs, $"\tdir={downloadDirectory}\n");
                                FileTools.AddText(fs, $"\tout={fileName}\n");
                            }
                            else if (downloadProgram == DownloadTools.IdmName)
                            {
                                FileTools.AddText(fs, $"/a /d \"{newlyReleaseDownloadUrl}\" /p \"{downloadDirectory}\" /f \"{fileName}\"\n");
                            }

                            string author = nodeList[i].SelectSingleNode("author") != null
                                ? nodeList[i].SelectSingleNode("author").InnerText
                                : nodeList[i].SelectSingleNode("itunes:author", nsmgr).InnerText;

                            string imageUrl = nodeList[i].SelectSingleNode("itunes:image", nsmgr).Attributes["href"].Value;

                            string description = RemoveStripHtml(nodeList[i].SelectSingleNode("description").InnerText);

                            string metadataFilePath = ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath + Path.DirectorySeparatorChar
                            +
                                                     fileName + ".metadata";
                            AudioMetadata.CreateAudioMetadata(metadataFilePath, title, newlyReleaseTitle, author, imageUrl, description,
                                dt);
                        }
                    }
                }
            }
        }

        public void GetPodcastAllReleaseDetail(ref List<string> outputStrings)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(FileName);

            var root = xmlDoc.DocumentElement;

            XmlNode channel = root.SelectSingleNode("channel");

            XmlNodeList nodeList = channel.ChildNodes;

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].Name == "item")
                {
                    string newlyReleasePubDate = nodeList[i].SelectSingleNode("pubDate").InnerText;
                    DateTime dt = DateTime.Parse(newlyReleasePubDate).ToUniversalTime();

                    string newlyReleaseTitle =
                        nodeList[i].SelectSingleNode("title").InnerText.Trim();

                    newlyReleasePubDate = dt.ToString("G");

                    string showString = $"{newlyReleaseTitle} - {newlyReleasePubDate}\n";

                    outputStrings.Add(showString);
                }
            }
        }

        public void DownloadSelectIndexRelease(int[] selectIndex, string downloadDirectory, bool isSimpleFile, string downloadProgram, ref FileStream fs)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(FileName);

            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd");

            var root = xmlDoc.DocumentElement;

            XmlNode channel = root.SelectSingleNode("channel");

            string title = channel.SelectSingleNode("title").InnerText;

            XmlNodeList nodeList = channel.ChildNodes;

            int index = 0;

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].Name == "item")
                {
                    index++;
                    if (selectIndex.Contains(index))
                    {
                        string newlyReleasePubDate = nodeList[i].SelectSingleNode("pubDate").InnerText;
                        DateTime dt = DateTime.Now;
                        dt = DateTime.Parse(newlyReleasePubDate).ToUniversalTime();

                        newlyReleasePubDate = dt.ToString("yyyy_MM_dd", DateTimeFormatInfo.InvariantInfo);

                        string newlyReleaseTitle =
                            nodeList[i].SelectSingleNode("title").InnerText.Trim();
                        string newlyReleaseDownloadUrl =
                            nodeList[i].SelectSingleNode("enclosure").Attributes["url"].Value;

                        Logger.Log.Info($"Get the release title is {newlyReleaseTitle}, time is {dt.ToString("G")}.");

                        if (isSimpleFile)
                        {
                            FileTools.AddText(fs, $"{newlyReleaseDownloadUrl}\n");
                        }
                        else
                        {
                            string fileExtension = ".mp3";
                            if (newlyReleaseDownloadUrl.Contains(".m4a"))
                            {
                                fileExtension = ".m4a";
                            }

                            string fileName = GetValidName($"{title} - {newlyReleaseTitle} - {newlyReleasePubDate}{fileExtension}");
                            if (downloadProgram == DownloadTools.Aria2Name)
                            {
                                FileTools.AddText(fs, $"{newlyReleaseDownloadUrl}\n");
                                FileTools.AddText(fs, $"\tdir={downloadDirectory}\n");
                                FileTools.AddText(fs, $"\tout={fileName}\n");
                            }
                            else if (downloadProgram == DownloadTools.IdmName)
                            {
                                FileTools.AddText(fs, $"/a /d \"{newlyReleaseDownloadUrl}\" /p \"{downloadDirectory}\" /f \"{fileName}\"\n");
                            }

                            string author = nodeList[i].SelectSingleNode("author") != null
                                ? nodeList[i].SelectSingleNode("author").InnerText
                                : nodeList[i].SelectSingleNode("itunes:author", nsmgr).InnerText;

                            string imageUrl = nodeList[i].SelectSingleNode("itunes:image", nsmgr).Attributes["href"].Value;

                            string description = RemoveStripHtml(nodeList[i].SelectSingleNode("description").InnerText);

                            string metadataFilePath = ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath + Path.DirectorySeparatorChar +
                                                      fileName + ".metadata";
                            AudioMetadata.CreateAudioMetadata(metadataFilePath, title, newlyReleaseTitle, author, imageUrl, description,
                                dt);
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

        private static string GetValidName(string name)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(name, "");
        }
    }
}