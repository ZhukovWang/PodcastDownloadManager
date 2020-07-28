using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

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

            string podcastName = GetPodcastName(url);

            if (!_podcastsDictionary.ContainsKey(podcastName))
            {
                _podcastsDictionary.Add(podcastName, url);

                SaveNewOpml();
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
                return 1;
            }
            else
            {
                _podcastsDictionary.Remove(name);

                SaveNewOpml();

                return 0;
            }
        }

        /// <summary>
        /// show the podcast list
        /// </summary>
        /// <param name="podcastList">all podcast information</param>
        public static void ListPodcast(out List<string> podcastList)
        {
            GetAllPodcast();

            podcastList = new List<string>();

            foreach (KeyValuePair<string, string> podcast in _podcastsDictionary)
            {
                string showString = String.Empty;

                showString = $"\tName: {podcast.Key}  Url: {podcast.Value}\n";

                podcastList.Add(showString);
            }
        }

        public static string ShowPodcastDetail(string name)
        {
            GetAllPodcast();

            string url = _podcastsDictionary[name];

            Podcast podcast = new Podcast(name, url);

            return podcast.GetPodcastDetail();
        }

        public static void UpdateAllPodcasts(out List<string> showString)
        {
            showString = new List<string>();

            GetAllPodcast();

            foreach (var podcast in _podcastsDictionary)
            {
                Podcast p = new Podcast(podcast.Key, podcast.Value, true);

                p.GetPodcastNewlyRelease(ref showString);
            }
        }

        public static void DownloadPodcastAfterDate(DateTime dt, string downloadFileDirectory, bool isSimpleFile, string downloadProgram)
        {
            GetAllPodcast();

            ProgramConfiguration.DownloadFileName = downloadFileDirectory + "\\" + "PodcastDownload.txt";

            FileStream fs = File.Create(ProgramConfiguration.DownloadFileName);

            foreach (var podcast in _podcastsDictionary)
            {
                Podcast p = new Podcast(podcast.Key, podcast.Value, true);

                p.BuildPodcastDownloadFile(dt, downloadFileDirectory, isSimpleFile, downloadProgram, ref fs);
            }

            fs.Close();
        }

        public static int ListPodcastAllRelease(string name, ref List<string> outputString)
        {
            GetAllPodcast();
            string url;

            try
            {
                url = _podcastsDictionary[name];
            }
            catch
            {
                outputString.Add("Error. Input of Name does not contain in the library.");
                return -1;
            }

            Podcast podcast = new Podcast(name, url);

            podcast.GetPodcastAllReleaseDetail(ref outputString);

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

            ProgramConfiguration.DownloadFileName = downloadFileDirectory + "\\" + "PodcastDownload.txt";

            FileStream fs = File.Create(ProgramConfiguration.DownloadFileName);

            podcast.DownloadSelectIndexRelease(selectIndex, downloadFileDirectory, isSimpleFile, downloadProgram, ref fs);

            fs.Close();

            return 0;
        }
    }
}