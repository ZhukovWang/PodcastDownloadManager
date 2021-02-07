using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NFlags;

namespace PodcastDownloadManager.Podcast
{
    public static class Opml
    {
        /*
         * Opml file example
         * <?xml version="1.0" encoding="utf-8" standalone="no"?>
            <opml version="1.0">
                <head>
                    <title>Pocket Casts Feeds</title>
                </head>
                <body>
                    <outline text="feeds">
                        <outline xmlUrl="https://feeds.npr.org/510282/podcast.xml" text="Pop Culture Happy Hour" type="rss" />
                        <outline xmlUrl="https://rss.wbur.org/modernlove/podcast" text="Modern Love" type="rss" />
                    </outline>
                </body>
            </opml>
         */

        private static Dictionary<string, string> _podcastsDictionary = new Dictionary<string, string>();

        /// <summary>
        /// Create a new opml
        /// </summary>
        public static void Create()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "utf-8", null));

            XmlElement opml = xmlDoc.CreateElement("opml");
            opml.SetAttribute("version", "1.0");
            xmlDoc.AppendChild(opml);

            XmlElement head = xmlDoc.CreateElement("head");
            opml.AppendChild(head);

            XmlElement opmlTitle = xmlDoc.CreateElement("title");
            opmlTitle.InnerText = "Podcast Download Manager Feeds";
            head.AppendChild(opmlTitle);

            XmlElement body = xmlDoc.CreateElement("body");
            opml.AppendChild(body);

            XmlElement outline = xmlDoc.CreateElement("outline");
            outline.SetAttribute("text", "feeds");
            body.AppendChild(outline);

            xmlDoc.Save(ProgramConfiguration.PodcastFileName);
        }

        private static void GetAllPodcast()
        {
            Logger.Log.Info("Get all have podcasts info.");

            _podcastsDictionary = new Dictionary<string, string>();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(ProgramConfiguration.PodcastFileName);

            XmlElement root = xmlDoc.DocumentElement;

            XmlNode outline = root.GetElementsByTagName("body")[0].FirstChild;

            XmlNodeList allPodcastNodes = outline.ChildNodes;

            for (int i = 0; i < allPodcastNodes.Count; i++)
            {
                string podcastName = allPodcastNodes[i].Attributes["text"].Value;
                string podcastUrl = allPodcastNodes[i].Attributes["xmlUrl"].Value;
                _podcastsDictionary.Add(podcastName, podcastUrl);
            }

            Logger.Log.Info("Finish get all have podcasts info.");
        }

        private static void SaveNewOpml()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "utf-8", null));

            XmlElement opml = xmlDoc.CreateElement("opml");
            opml.SetAttribute("version", "1.0");
            xmlDoc.AppendChild(opml);

            XmlElement head = xmlDoc.CreateElement("head");
            opml.AppendChild(head);

            XmlElement opmlTitle = xmlDoc.CreateElement("title");
            opmlTitle.InnerText = "Podcast Download Manager Feeds";
            head.AppendChild(opmlTitle);

            XmlElement body = xmlDoc.CreateElement("body");
            opml.AppendChild(body);

            XmlElement outline = xmlDoc.CreateElement("outline");
            outline.SetAttribute("text", "feeds");
            body.AppendChild(outline);

            foreach (KeyValuePair<string, string> podcast in _podcastsDictionary)
            {
                XmlElement newPodcast = xmlDoc.CreateElement("outline");
                newPodcast.SetAttribute("xmlUrl", podcast.Value);
                newPodcast.SetAttribute("text", podcast.Key);
                outline.AppendChild(newPodcast);
            }

            xmlDoc.Save(ProgramConfiguration.PodcastFileName);
        }

        private static string GetPodcastName(string url)
        {
            Podcast podcast = new Podcast(url);

            return podcast.Name;
        }

        /// <summary>
        /// Add a new podcast
        /// </summary>
        /// <param name="url">podcast url</param>
        /// <returns>podcast name</returns>
        public static string AddPodcast(string url)
        {
            GetAllPodcast();

            Logger.Log.Info("Get the url's name.");
            string podcastName = GetPodcastName(url);
            Logger.Log.Info($"Finish get the url's name. Podcast name is {podcastName}");

            if (!_podcastsDictionary.ContainsKey(podcastName))
            {
                _podcastsDictionary.Add(podcastName, url);
                Logger.Log.Info("Add the podcast to dictionary.");

                Logger.Log.Info("Save the new opml file.");
                SaveNewOpml();
                Logger.Log.Info("Finish save the new opml file.");
            }
            else
            {
                Logger.Log.Info("The podcast is already in the opml file.");
            }

            return podcastName;
        }

        /// <summary>
        /// remove a specific podcast through name
        /// </summary>
        /// <param name="name"></param>
        /// <returns>0: had removed; 1: there is not this podcast.</returns>
        public static int RemovePodcast(string name)
        {
            GetAllPodcast();

            if (!_podcastsDictionary.ContainsKey(name))
            {
                Logger.Log.Info($"The podcast does not exist in the dictionary. Name is {name}.");
                return 1;
            }
            else
            {
                Logger.Log.Info($"Remove the podcast from dictionary. Name is {name}.");
                _podcastsDictionary.Remove(name);

                Logger.Log.Info("Save the new opml file.");
                SaveNewOpml();
                Logger.Log.Info("Finish save the new opml file.");

                return 0;
            }
        }

        /// <summary>
        /// show the podcast list
        /// </summary>
        public static void ListPodcast(ref IOutput output)
        {
            GetAllPodcast();

            foreach (KeyValuePair<string, string> podcast in _podcastsDictionary)
            {
                var showString = $"* Name: {podcast.Key}  Url: {podcast.Value}";

                output.WriteLine(showString);
            }
        }

        public static string ShowPodcastDetail(string name)
        {
            GetAllPodcast();
            string url;
            Logger.Log.Info("Get the name's podcast info.");

            try
            {
                url = _podcastsDictionary[name];
            }
            catch
            {
                Logger.Log.Warn("The name is not in the dictionary.");
                return "Error.Input of Name does not contain in the library.";
            }

            Podcast podcast = new Podcast(name, url);

            Logger.Log.Info("Get the podcast detail.");
            return podcast.GetPodcastDetail();
        }

        public static int UpdateAllPodcasts(ref IOutput output)
        {
            int updateCount = 0;

            GetAllPodcast();

            foreach (var podcast in _podcastsDictionary)
            {
                Podcast p = new Podcast(podcast.Key, podcast.Value, true);

                p.GetPodcastNewlyRelease(ref output, ref updateCount);
            }

            return updateCount;
        }

        public static void DownloadPodcastAfterDate(DateTime dt, string downloadFileDirectory, bool isSimpleFile, string downloadProgram)
        {
            GetAllPodcast();

            ProgramConfiguration.DownloadFileName = downloadFileDirectory + Path.DirectorySeparatorChar + "PodcastDownload.txt";

            FileStream fs = File.Create(ProgramConfiguration.DownloadFileName);

            foreach (var podcast in _podcastsDictionary)
            {
                Podcast p = new Podcast(podcast.Key, podcast.Value, true);

                p.BuildPodcastDownloadFile(dt, downloadFileDirectory, isSimpleFile, downloadProgram, ref fs);
            }

            fs.Close();
        }

        public static int ListPodcastAllRelease(string name, ref IOutput output)
        {
            GetAllPodcast();
            string url;

            try
            {
                url = _podcastsDictionary[name];
            }
            catch
            {
                output.WriteLine("Error. Input of Name does not contain in the library.");
                return -1;
            }

            Podcast podcast = new Podcast(name, url);

            podcast.GetPodcastAllReleaseDetail(ref output);

            return 0;
        }

        public static int DownloadPodcastSelectRelease(string name, int[] selectIndex, string downloadFileDirectory, bool isSimpleFile, string downloadProgram)
        {
            GetAllPodcast();

            string url;
            try
            {
                url = _podcastsDictionary[name];
            }
            catch
            {
                return -1;
            }

            Podcast podcast = new Podcast(name, url);

            ProgramConfiguration.DownloadFileName = downloadFileDirectory + Path.DirectorySeparatorChar + "PodcastDownload.txt";

            Logger.Log.Info($"Create DownloadFile, is {ProgramConfiguration.DownloadFileName}.");

            FileStream fs = File.Create(ProgramConfiguration.DownloadFileName);

            podcast.DownloadSelectIndexRelease(selectIndex, downloadFileDirectory, isSimpleFile, downloadProgram, ref fs);

            fs.Close();

            return 0;
        }
    }
}