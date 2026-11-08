using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        private const int MaxConcurrentFeedDownloads = 8;

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

            if (!File.Exists(ProgramConfiguration.PodcastFileName))
            {
                Logger.Log.Warn($"The opml file does not exist. Path is {ProgramConfiguration.PodcastFileName}.");
                return;
            }

            var xmlDoc = new XmlDocument { XmlResolver = null };

            using (var reader = XmlReader.Create(ProgramConfiguration.PodcastFileName, new XmlReaderSettings
                   {
                       DtdProcessing = DtdProcessing.Prohibit,
                       XmlResolver = null,
                       IgnoreComments = true,
                   }))
            {
                xmlDoc.Load(reader);
            }

            XmlNodeList bodyNodes = xmlDoc.DocumentElement?.GetElementsByTagName("body");

            XmlNode outline = bodyNodes != null && bodyNodes.Count > 0 ? bodyNodes[0].FirstChild : null;

            if (outline == null)
            {
                Logger.Log.Warn("The opml file does not have any outline.");
                return;
            }

            foreach (XmlNode node in outline.ChildNodes)
            {
                string podcastName = node.Attributes?["text"]?.Value;
                string podcastUrl = node.Attributes?["xmlUrl"]?.Value;

                if (string.IsNullOrEmpty(podcastName) || string.IsNullOrEmpty(podcastUrl))
                {
                    Logger.Log.Warn("An outline without 'text' or 'xmlUrl' was skipped.");
                    continue;
                }

                // Indexer instead of Add: a hand-edited opml can contain the same name twice, and
                // that should update the url rather than throw.
                _podcastsDictionary[podcastName] = podcastUrl;
            }

            Logger.Log.Info($"Finish get all have podcasts info. {_podcastsDictionary.Count} podcast(s).");
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
        /// <returns>podcast name, or null when the feed could not be read</returns>
        public static string AddPodcast(string url)
        {
            GetAllPodcast();

            Logger.Log.Info("Get the url's name.");
            string podcastName = GetPodcastName(url);
            Logger.Log.Info($"Finish get the url's name. Podcast name is {podcastName}");

            if (string.IsNullOrEmpty(podcastName))
            {
                Logger.Log.Warn($"The feed of {url} could not be read, nothing was added.");
                return null;
            }

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

        /// <summary>
        /// Shows the detail of a podcast.
        /// </summary>
        /// <param name="name">Podcast name.</param>
        /// <param name="refresh">
        /// When false the cached feed is reused, so 'show' does not cost a network round trip. Pass
        /// true to download the feed first.
        /// </param>
        public static string ShowPodcastDetail(string name, bool refresh = false)
        {
            GetAllPodcast();

            if (!_podcastsDictionary.TryGetValue(name, out string url))
            {
                Logger.Log.Warn("The name is not in the dictionary.");
                return "Error.Input of Name does not contain in the library.";
            }

            Podcast podcast = new Podcast(name, url, !refresh && Podcast.HasCache(name));

            if (!podcast.FeedAvailable())
            {
                return "Error. The feed of this podcast is not available. Run 'pdlm update' first.";
            }

            Logger.Log.Info("Get the podcast detail.");
            return podcast.GetPodcastDetail();
        }

        /// <summary>
        /// Refreshes every feed, then reports and queues the new releases.
        /// </summary>
        /// <remarks>
        /// The feeds are downloaded concurrently (they are independent and network bound), but the
        /// results are reported and appended in library order on a single thread: the download list
        /// is one shared file, so writing it in parallel would only add locking.
        /// </remarks>
        public static int UpdateAllPodcasts(ref IOutput output)
        {
            GetAllPodcast();

            if (_podcastsDictionary.Count == 0)
            {
                output.WriteLine("The library is empty. Use 'pdlm add <url>' first.");
                return 0;
            }

            var feeds = new List<KeyValuePair<string, string>>(_podcastsDictionary);

            int concurrency = Math.Min(feeds.Count, Math.Min(MaxConcurrentFeedDownloads, Math.Max(1, System.Environment.ProcessorCount * 2)));

            Logger.Log.Info($"Update {feeds.Count} podcast(s) with {concurrency} concurrent download(s).");

            var results = new FeedUpdateResult[feeds.Count];

            using (var gate = new SemaphoreSlim(concurrency, concurrency))
            {
                var tasks = new Task[feeds.Count];

                for (int i = 0; i < feeds.Count; i++)
                {
                    int index = i;
                    Podcast podcast = new Podcast(feeds[index].Key, feeds[index].Value, true);

                    tasks[index] = Task.Run(async () =>
                    {
                        await gate.WaitAsync().ConfigureAwait(false);

                        try
                        {
                            results[index] = podcast.CollectNewlyReleases();
                        }
                        catch (Exception e)
                        {
                            Logger.Log.Error($"Update {feeds[index].Key} failed. Exception is {e}");
                            results[index] = new FeedUpdateResult
                            {
                                PodcastName = feeds[index].Key,
                                Url = feeds[index].Value,
                                Error = e.Message,
                            };
                        }
                        finally
                        {
                            gate.Release();
                        }
                    });
                }

                Task.WhenAll(tasks).GetAwaiter().GetResult();
            }

            var pending = new List<NewlyRelease>();

            foreach (FeedUpdateResult result in results)
            {
                if (result == null)
                {
                    continue;
                }

                if (!result.Success)
                {
                    output.WriteLine($"* Failed to update {result.PodcastName}: {result.Error}");
                    continue;
                }

                foreach (NewlyRelease release in result.Releases)
                {
                    string published = release.PublishedUtc.ToString("yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo);

                    output.WriteLine($"* {release.PodcastName} - {release.Title} - {published}");

                    pending.Add(release);
                    Podcast.WriteMetadata(ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath, release);
                }
            }

            if (pending.Count > 0)
            {
                AppendDownloadEntries(pending);
            }

            Logger.Log.Info($"Update finished. {pending.Count} new release(s).");

            return pending.Count;
        }

        private static void AppendDownloadEntries(List<NewlyRelease> releases)
        {
            string downloadProgram = ProgramConfiguration.DownloadConfigurations.DownloadProgram;
            string downloadPath = ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath;

            using var fs = new FileStream(ProgramConfiguration.PodcastNewlyReleaseInfo, FileMode.Append,
                FileAccess.Write, FileShare.Read);

            foreach (NewlyRelease release in releases)
            {
                Podcast.AppendDownloadEntry(fs, downloadProgram, downloadPath, release.DownloadUrl, release.FileName);
            }
        }

        public static void DownloadPodcastAfterDate(DateTime dt, string downloadFileDirectory, bool isSimpleFile, string downloadProgram)
        {
            GetAllPodcast();

            ProgramConfiguration.DownloadFileName = Path.Combine(downloadFileDirectory, "PodcastDownload.txt");

            FileStream fs = File.Create(ProgramConfiguration.DownloadFileName);

            try
            {
                foreach (var podcast in _podcastsDictionary)
                {
                    Podcast p = new Podcast(podcast.Key, podcast.Value, Podcast.HasCache(podcast.Key));

                    p.BuildPodcastDownloadFile(dt, downloadFileDirectory, isSimpleFile, downloadProgram, ref fs);
                }
            }
            finally
            {
                fs.Close();
            }
        }

        public static int ListPodcastAllRelease(string name, ref IOutput output)
        {
            GetAllPodcast();

            if (!_podcastsDictionary.TryGetValue(name, out string url))
            {
                output.WriteLine("Error. Input of Name does not contain in the library.");
                return -1;
            }

            Podcast podcast = new Podcast(name, url, Podcast.HasCache(name));

            if (!podcast.FeedAvailable())
            {
                output.WriteLine("Error. The feed of this podcast is not available. Run 'pdlm update' first.");
                return -1;
            }

            podcast.GetPodcastAllReleaseDetail(ref output);

            return 0;
        }

        public static int DownloadPodcastSelectRelease(string name, int[] selectIndex, string downloadFileDirectory, bool isSimpleFile, string downloadProgram)
        {
            GetAllPodcast();

            if (!_podcastsDictionary.TryGetValue(name, out string url))
            {
                return -1;
            }

            Podcast podcast = new Podcast(name, url, Podcast.HasCache(name));

            if (!podcast.FeedAvailable())
            {
                return -1;
            }

            ProgramConfiguration.DownloadFileName = Path.Combine(downloadFileDirectory, "PodcastDownload.txt");

            Logger.Log.Info($"Create DownloadFile, is {ProgramConfiguration.DownloadFileName}.");

            FileStream fs = File.Create(ProgramConfiguration.DownloadFileName);

            try
            {
                podcast.DownloadSelectIndexRelease(selectIndex, downloadFileDirectory, isSimpleFile, downloadProgram, ref fs);
            }
            finally
            {
                fs.Close();
            }

            return 0;
        }
    }
}
