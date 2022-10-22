using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using PodcastDownloadManager.FileMetadata;
using NFlags;

namespace PodcastDownloadManager.Podcast
{
    public class Podcast
    {
        private readonly string _url;

        public readonly string Name;

        private readonly string _fileName;

        public Podcast(string url)
        {
            this._url = url;
            _fileName = $"{ProgramConfiguration.PodcastsFileDirectory}{Path.DirectorySeparatorChar}NewPodcast.xml";
            Download(url);
            this.Name = GetPodcastName();
            File.Delete(_fileName);
        }

        public Podcast(string name, string url, bool skipDownload = false)
        {
            this._url = url;
            this.Name = name;
            _fileName = $"{ProgramConfiguration.PodcastsFileDirectory}{Path.DirectorySeparatorChar}{name}.xml";
            if (!skipDownload)
            {
                Download(url);
            }
        }

        private void Download(string url)
        {
            if (File.Exists(_fileName))
            {
                File.Delete(_fileName);
            }

            Logger.Log.Info($"Start download podcast xml file. Url is {url}.");
            try
            {
                DownloadHelper.DownloadFileAsync(url, _fileName).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Download {_fileName} failed.");
                Logger.Log.Error($"Download failed. File path is {_fileName}. Exception is {e}");
                return;
            }
            Logger.Log.Info($"Download finish. File path is {_fileName}.");
        }

        private string GetPodcastName()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(_fileName);

            var root = xmlDoc.DocumentElement;

            XmlNode channel = root.SelectSingleNode("channel");

            XmlNode titleNode = channel.SelectSingleNode("title");

            string name = titleNode.InnerText.Trim().Replace(" ", "");

            return GetValidName(name);
        }

        public string GetPodcastDetail()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(_fileName);

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

        public void GetPodcastNewlyRelease(ref IOutput output, ref int updateCount)
        {
            //Download new xml
            Logger.Log.Info($"Download the new file of {Name}.");

            try
            {
                DownloadHelper.DownloadFileAsync(this._url,  $"{_fileName}.tmp").Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Download {Name} failed.");
                Logger.Log.Error($"Download {Name} failed. Exception is {e}");
                return;
            }

            Logger.Log.Info($"Finish download the new file of {Name}.");

            //get last update time
            DateTime lastUpdateDateTime = DateTime.Now;
            if (File.Exists(_fileName))
            {
                lastUpdateDateTime = File.GetLastWriteTimeUtc(_fileName);
                File.Delete($"{_fileName}");
            }
            Logger.Log.Info($"Get the {Name} last update time is {lastUpdateDateTime.ToString("G")}.");

            File.Copy($"{_fileName}.tmp", _fileName, true);
            File.Delete($"{_fileName}.tmp");

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(_fileName);

            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd");

            var root = xmlDoc.DocumentElement;

            if (root == null)
            {
                output.WriteLine("The xml file don't have 'root'.");
                return;
            }

            XmlNode channel = root.SelectSingleNode("channel");

            if (channel == null)
            {
                output.WriteLine("The xml file don't have 'channel'.");
                return;
            }

            XmlNode titleNode = channel.SelectSingleNode("title");

            if (titleNode == null)
            {
                output.WriteLine("The xml file don't have 'title'.");
                return;
            }

            string title = titleNode.InnerText;

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

                        output.WriteLine(showString);
                        updateCount++;

                        newlyReleasePubDate = dt.ToString("yyyy_MM_dd", DateTimeFormatInfo.InvariantInfo);

                        string newlyReleaseDownloadUrl =
                            nodeList[i].SelectSingleNode("enclosure").Attributes["url"].Value;

                        string fileExtension = ".mp3";
                        if (newlyReleaseDownloadUrl.Contains(".m4a"))
                        {
                            fileExtension = ".m4a";
                        }

                        string fileName = GetValidName($"{title} - {newlyReleasePubDate} - {newlyReleaseTitle}{fileExtension}");

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

                        string imageUrl = "null";
                        if (nodeList[i].SelectSingleNode("itunes:image", nsmgr) != null)
                        {
                            imageUrl = nodeList[i].SelectSingleNode("itunes:image", nsmgr).Attributes["href"].Value;
                        }

                        string description = "null";
                        if (nodeList[i].SelectSingleNode("description") != null)
                        {
                            description = RemoveStripHtml(nodeList[i].SelectSingleNode("description").InnerText);
                        }

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
            xmlDoc.Load(_fileName);

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

                            string fileName = GetValidName($"{title} - {newlyReleasePubDate} - {newlyReleaseTitle}{fileExtension}");
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
                                : nodeList[i].SelectSingleNode("itunes:author", nsmgr) != null
                                    ? nodeList[i].SelectSingleNode("itunes:author", nsmgr).InnerText
                                    : "None";

                            string imageUrl = nodeList[i].SelectSingleNode("itunes:image", nsmgr) != null
                                ? nodeList[i].SelectSingleNode("itunes:image", nsmgr).Attributes["href"].Value
                                : channel.SelectSingleNode("itunes:image", nsmgr).InnerText;

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

        public void GetPodcastAllReleaseDetail(ref IOutput output)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(_fileName);

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

                    string showString = $"* {newlyReleaseTitle} - {newlyReleasePubDate}";

                    output.WriteLine(showString);
                }
            }
        }

        public void DownloadSelectIndexRelease(int[] selectIndex, string downloadDirectory, bool isSimpleFile, string downloadProgram, ref FileStream fs)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(_fileName);

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
                        DateTime dt = DateTime.Parse(newlyReleasePubDate).ToUniversalTime();

                        newlyReleasePubDate = dt.ToString("yyyy_MM_dd", DateTimeFormatInfo.InvariantInfo);

                        string newlyReleaseTitle = nodeList[i].SelectSingleNode("title").InnerText.Trim();
                        string newlyReleaseDownloadUrl = nodeList[i].SelectSingleNode("enclosure").Attributes["url"].Value;

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

                            string fileName = GetValidName($"{title} - {newlyReleasePubDate} - {newlyReleaseTitle}{fileExtension}");
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