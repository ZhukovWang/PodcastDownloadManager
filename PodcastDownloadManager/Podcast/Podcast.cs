using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using PodcastDownloadManager.FileMetadata;
using NFlags;

namespace PodcastDownloadManager.Podcast
{
    /// <summary>
    /// One release that is newer than the last update of its feed.
    /// </summary>
    public sealed class NewlyRelease
    {
        public string PodcastName;

        public string PodcastTitle;

        public string Title;

        public string DownloadUrl;

        public string FileName;

        public string Author;

        public string ImageUrl;

        public string Description;

        public DateTime PublishedUtc;
    }

    /// <summary>
    /// Outcome of refreshing one feed. Errors are carried in <see cref="Error"/> instead of being
    /// thrown, so that one broken feed cannot abort an update of the whole library.
    /// </summary>
    public sealed class FeedUpdateResult
    {
        public string PodcastName;

        public string Url;

        public bool Success;

        public string Error;

        public readonly List<NewlyRelease> Releases = new List<NewlyRelease>();
    }

    public class Podcast
    {
        private const string ItunesNamespace = "http://www.itunes.com/dtds/podcast-1.0.dtd";

        private static readonly XmlReaderSettings FeedReaderSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        private readonly string _url;

        public readonly string Name;

        private readonly string _fileName;

        /// <summary>
        /// Reads the name of an unknown podcast from its feed. <see cref="Name"/> is null when the
        /// feed could not be downloaded or parsed.
        /// </summary>
        public Podcast(string url)
        {
            _url = url;
            _fileName = Path.Combine(ProgramConfiguration.PodcastsFileDirectory, "NewPodcast.xml");

            Name = TryDownloadFeed() ? GetPodcastName() : null;

            TryDeleteFile(_fileName);
        }

        public Podcast(string name, string url, bool skipDownload = false)
        {
            _url = url;
            Name = name;
            _fileName = GetCachePath(name);

            if (!skipDownload)
            {
                TryDownloadFeed();
            }
        }

        /// <summary>
        /// Where the feed of <paramref name="name"/> is cached.
        /// </summary>
        public static string GetCachePath(string name)
        {
            return Path.Combine(ProgramConfiguration.PodcastsFileDirectory, $"{name}.xml");
        }

        /// <summary>
        /// Whether a usable copy of the feed is already on disk.
        /// </summary>
        public static bool HasCache(string name)
        {
            return !string.IsNullOrEmpty(name) && File.Exists(GetCachePath(name));
        }

        /// <summary>
        /// Whether the feed of this podcast is available locally.
        /// </summary>
        public bool FeedAvailable()
        {
            return File.Exists(_fileName);
        }

        private bool TryDownloadFeed()
        {
            Logger.Log.Info($"Start download podcast xml file. Url is {_url}.");

            try
            {
                DownloadHelper.DownloadFileAsync(_url, _fileName).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Download \"{_url}\" failed.");
                Logger.Log.Error($"Download failed. File path is {_fileName}. Exception is {e}");
                return false;
            }

            Logger.Log.Info($"Download finish. File path is {_fileName}.");

            return true;
        }

        private string GetPodcastName()
        {
            XmlDocument xmlDoc;
            try
            {
                xmlDoc = LoadFeed();
            }
            catch (Exception e)
            {
                Logger.Log.Error($"The feed of {_url} is not valid xml. Exception is {e}");
                return null;
            }

            string name = GetChildText(GetChannel(xmlDoc), "title");

            if (string.IsNullOrWhiteSpace(name))
            {
                Logger.Log.Error($"The feed of {_url} does not have a channel title.");
                return null;
            }

            return GetValidName(name.Trim().Replace(" ", ""));
        }

        public string GetPodcastDetail()
        {
            if (!FeedAvailable())
            {
                return "Error. The feed of this podcast is not available. Run 'pdlm update' first.";
            }

            XmlDocument xmlDoc = LoadFeed();
            XmlNode channel = GetChannel(xmlDoc);

            string title = GetChildText(channel, "title") ?? Name;
            string description = RemoveStripHtml(GetChildText(channel, "description") ?? string.Empty);

            XmlNode newlyRelease = GetReleaseNodes(channel).FirstOrDefault();

            if (newlyRelease == null)
            {
                return $"Title: {title}\n" +
                       $"Description: {description}\n\n" +
                       "Newly release: none";
            }

            string newlyReleaseTitle = GetChildText(newlyRelease, "title") ?? string.Empty;
            string newlyReleaseDescription = RemoveStripHtml(GetChildText(newlyRelease, "description") ?? string.Empty);
            string newlyReleaseDownloadUrl = GetAttribute(newlyRelease, "enclosure", "url") ?? "null";

            string newlyReleasePubDate = TryParseUtc(GetChildText(newlyRelease, "pubDate"), out DateTime dt)
                ? dt.ToString("G")
                : "unknown";

            return $"Title: {title}\n" +
                   $"Description: {description}\n\n" +
                   "Newly release:\n" +
                   $"Title: {newlyReleaseTitle}\n" +
                   $"Description: {newlyReleaseDescription}\n" +
                   $"Public date: {newlyReleasePubDate}\n" +
                   $"Download url: {newlyReleaseDownloadUrl}";
        }

        /// <summary>
        /// Downloads the feed and collects every release published after the cached feed was written.
        /// Contains no shared state, so it is safe to run for several podcasts in parallel.
        /// </summary>
        public FeedUpdateResult CollectNewlyReleases()
        {
            var result = new FeedUpdateResult { PodcastName = Name, Url = _url };

            // Read the timestamp before downloading: a successful download replaces the cached file
            // and resets its last write time to "now", which would forget the "new since last update"
            // mark. Both sides of the comparison are UTC; mixing in local time silently shifts the
            // window by the time zone offset.
            DateTime lastUpdateUtc = FeedAvailable() ? File.GetLastWriteTimeUtc(_fileName) : DateTime.UtcNow;

            Logger.Log.Info($"Download the new file of {Name}. Last update is {lastUpdateUtc:G} (UTC).");

            try
            {
                DownloadHelper.DownloadFileAsync(_url, _fileName).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                result.Error = e.Message;
                Logger.Log.Error($"Download {Name} failed. Exception is {e}");
                return result;
            }

            Logger.Log.Info($"Finish download the new file of {Name}.");

            XmlDocument xmlDoc;
            try
            {
                xmlDoc = LoadFeed();
            }
            catch (Exception e)
            {
                result.Error = $"The feed is not valid xml: {e.Message}";
                Logger.Log.Error($"The feed of {Name} is not valid xml. Exception is {e}");
                return result;
            }

            XmlNode channel = GetChannel(xmlDoc);

            if (channel == null)
            {
                result.Error = "The xml file does not have a 'channel' node.";
                return result;
            }

            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("itunes", ItunesNamespace);

            string title = GetChildText(channel, "title") ?? Name;

            foreach (XmlNode item in GetReleaseNodes(channel))
            {
                if (!TryParseUtc(GetChildText(item, "pubDate"), out DateTime dt))
                {
                    Logger.Log.Warn($"{Name}: an item without a readable pubDate was skipped.");
                    continue;
                }

                if (dt <= lastUpdateUtc)
                {
                    continue;
                }

                string newlyReleaseTitle = (GetChildText(item, "title") ?? string.Empty).Trim();
                string newlyReleaseDownloadUrl = GetAttribute(item, "enclosure", "url");

                if (string.IsNullOrEmpty(newlyReleaseDownloadUrl))
                {
                    Logger.Log.Warn($"{Name}: \"{newlyReleaseTitle}\" has no enclosure url, skipped.");
                    continue;
                }

                result.Releases.Add(new NewlyRelease
                {
                    PodcastName = Name,
                    PodcastTitle = title,
                    Title = newlyReleaseTitle,
                    DownloadUrl = newlyReleaseDownloadUrl,
                    FileName = BuildFileName(title, dt, newlyReleaseTitle, newlyReleaseDownloadUrl),
                    Author = GetChildText(item, "author")
                             ?? GetChildText(item, "itunes:author", nsmgr)
                             ?? title,
                    ImageUrl = GetAttribute(item, "itunes:image", "href", nsmgr) ?? "null",
                    Description = RemoveStripHtml(GetChildText(item, "description") ?? "null"),
                    PublishedUtc = dt,
                });

                Logger.Log.Info($"Get the newly release title is {newlyReleaseTitle}, time is {dt:G}.");
            }

            result.Success = true;
            return result;
        }

        public void BuildPodcastDownloadFile(DateTime dateTime, string downloadDirectory, bool isSimpleFile,
            string downloadProgram, ref FileStream fs)
        {
            XmlDocument xmlDoc = LoadFeed();

            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("itunes", ItunesNamespace);

            XmlNode channel = GetChannel(xmlDoc);
            string title = GetChildText(channel, "title") ?? Name;

            foreach (XmlNode item in GetReleaseNodes(channel))
            {
                if (!TryParseUtc(GetChildText(item, "pubDate"), out DateTime dt))
                {
                    Logger.Log.Warn($"{Name}: an item without a readable pubDate was skipped.");
                    continue;
                }

                if (dt <= dateTime)
                {
                    continue;
                }

                string newlyReleaseTitle = (GetChildText(item, "title") ?? string.Empty).Trim();
                string newlyReleaseDownloadUrl = GetAttribute(item, "enclosure", "url");

                if (string.IsNullOrEmpty(newlyReleaseDownloadUrl))
                {
                    continue;
                }

                Logger.Log.Info($"Get the newly release title is {newlyReleaseTitle}, time is {dt:G}.");

                if (isSimpleFile)
                {
                    FileTools.AddText(fs, $"{newlyReleaseDownloadUrl}\n");
                    continue;
                }

                string fileName = BuildFileName(title, dt, newlyReleaseTitle, newlyReleaseDownloadUrl);

                AppendDownloadEntry(fs, downloadProgram, downloadDirectory, newlyReleaseDownloadUrl, fileName);

                WriteMetadata(downloadDirectory, new NewlyRelease
                {
                    PodcastName = Name,
                    PodcastTitle = title,
                    Title = newlyReleaseTitle,
                    DownloadUrl = newlyReleaseDownloadUrl,
                    FileName = fileName,
                    Author = GetChildText(item, "author")
                             ?? GetChildText(item, "itunes:author", nsmgr)
                             ?? "None",
                    ImageUrl = GetAttribute(item, "itunes:image", "href", nsmgr) ?? "null",
                    Description = RemoveStripHtml(GetChildText(item, "description") ?? "null"),
                    PublishedUtc = dt,
                });
            }
        }

        public void GetPodcastAllReleaseDetail(ref IOutput output)
        {
            XmlDocument xmlDoc = LoadFeed();
            XmlNode channel = GetChannel(xmlDoc);

            foreach (XmlNode item in GetReleaseNodes(channel))
            {
                string newlyReleaseTitle = (GetChildText(item, "title") ?? string.Empty).Trim();

                string newlyReleasePubDate = TryParseUtc(GetChildText(item, "pubDate"), out DateTime dt)
                    ? dt.ToString("G")
                    : "unknown";

                output.WriteLine($"* {newlyReleaseTitle} - {newlyReleasePubDate}");
            }
        }

        public void DownloadSelectIndexRelease(int[] selectIndex, string downloadDirectory, bool isSimpleFile,
            string downloadProgram, ref FileStream fs)
        {
            XmlDocument xmlDoc = LoadFeed();

            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("itunes", ItunesNamespace);

            XmlNode channel = GetChannel(xmlDoc);
            string title = GetChildText(channel, "title") ?? Name;

            int index = 0;

            foreach (XmlNode item in GetReleaseNodes(channel))
            {
                index++;

                if (!selectIndex.Contains(index))
                {
                    continue;
                }

                if (!TryParseUtc(GetChildText(item, "pubDate"), out DateTime dt))
                {
                    Logger.Log.Warn($"{Name}: release {index} has no readable pubDate, skipped.");
                    continue;
                }

                string newlyReleaseTitle = (GetChildText(item, "title") ?? string.Empty).Trim();
                string newlyReleaseDownloadUrl = GetAttribute(item, "enclosure", "url");

                if (string.IsNullOrEmpty(newlyReleaseDownloadUrl))
                {
                    Logger.Log.Warn($"{Name}: release {index} has no enclosure url, skipped.");
                    continue;
                }

                Logger.Log.Info($"Get the release title is {newlyReleaseTitle}, time is {dt:G}.");

                if (isSimpleFile)
                {
                    FileTools.AddText(fs, $"{newlyReleaseDownloadUrl}\n");
                    continue;
                }

                string fileName = BuildFileName(title, dt, newlyReleaseTitle, newlyReleaseDownloadUrl);

                AppendDownloadEntry(fs, downloadProgram, downloadDirectory, newlyReleaseDownloadUrl, fileName);

                WriteMetadata(downloadDirectory, new NewlyRelease
                {
                    PodcastName = Name,
                    PodcastTitle = title,
                    Title = newlyReleaseTitle,
                    DownloadUrl = newlyReleaseDownloadUrl,
                    FileName = fileName,
                    Author = GetChildText(item, "author")
                             ?? GetChildText(item, "itunes:author", nsmgr)
                             ?? "None",
                    ImageUrl = GetAttribute(item, "itunes:image", "href", nsmgr) ?? "null",
                    Description = RemoveStripHtml(GetChildText(item, "description") ?? "null"),
                    PublishedUtc = dt,
                });
            }
        }

        /// <summary>
        /// Writes the metadata sidecar that 'pdlm autometadata' later turns into audio tags.
        /// </summary>
        internal static void WriteMetadata(string downloadDirectory, NewlyRelease release)
        {
            try
            {
                string metadataFilePath = Path.Combine(downloadDirectory, $"{release.FileName}.metadata");

                AudioMetadata.CreateAudioMetadata(metadataFilePath, release.PodcastTitle, release.Title,
                    release.Author, release.ImageUrl, release.Description, release.PublishedUtc);
            }
            catch (Exception e)
            {
                Logger.Log.Error($"Create metadata of \"{release.FileName}\" failed. Exception is {e}");
            }
        }

        /// <summary>
        /// Appends one download job for <paramref name="downloadProgram"/> to the download list.
        /// </summary>
        internal static void AppendDownloadEntry(FileStream fs, string downloadProgram, string downloadDirectory,
            string downloadUrl, string fileName)
        {
            if (downloadProgram == DownloadTools.Aria2Name)
            {
                FileTools.AddText(fs, $"{downloadUrl}\n");
                FileTools.AddText(fs, $"\tdir={downloadDirectory}\n");
                FileTools.AddText(fs, $"\tout={fileName}\n");
            }
            else if (downloadProgram == DownloadTools.IdmName)
            {
                FileTools.AddText(fs,
                    $"/a /d \"{downloadUrl}\" /p \"{downloadDirectory}\" /f \"{fileName}\"\n");
            }
        }

        private static string BuildFileName(string podcastTitle, DateTime publishedUtc, string releaseTitle,
            string downloadUrl)
        {
            string published = publishedUtc.ToString("yyyy_MM_dd", DateTimeFormatInfo.InvariantInfo);

            return GetValidName($"{podcastTitle} - {published} - {releaseTitle}{GetFileExtension(downloadUrl)}");
        }

        private static string GetFileExtension(string downloadUrl)
        {
            return downloadUrl.Contains(".m4a", StringComparison.OrdinalIgnoreCase) ? ".m4a" : ".mp3";
        }

        private XmlDocument LoadFeed()
        {
            var xmlDoc = new XmlDocument { XmlResolver = null };

            using var reader = XmlReader.Create(_fileName, FeedReaderSettings);
            xmlDoc.Load(reader);

            return xmlDoc;
        }

        private static XmlNode GetChannel(XmlDocument xmlDoc)
        {
            return xmlDoc?.DocumentElement?.SelectSingleNode("channel");
        }

        /// <summary>
        /// The 'item' children of a channel, in feed order.
        /// </summary>
        private static IEnumerable<XmlNode> GetReleaseNodes(XmlNode channel)
        {
            IEnumerable<XmlNode> items = channel?.ChildNodes.Cast<XmlNode>().Where(node => node.Name == "item");

            return items ?? Enumerable.Empty<XmlNode>();
        }

        private static string GetChildText(XmlNode parent, string name, XmlNamespaceManager nsmgr = null)
        {
            if (parent == null)
            {
                return null;
            }

            XmlNode node = nsmgr == null ? parent.SelectSingleNode(name) : parent.SelectSingleNode(name, nsmgr);

            return node?.InnerText;
        }

        private static string GetAttribute(XmlNode parent, string element, string attribute,
            XmlNamespaceManager nsmgr = null)
        {
            if (parent == null)
            {
                return null;
            }

            XmlNode node = nsmgr == null ? parent.SelectSingleNode(element) : parent.SelectSingleNode(element, nsmgr);

            return node?.Attributes?[attribute]?.Value;
        }

        private static bool TryParseUtc(string value, out DateTime utc)
        {
            utc = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out DateTimeOffset parsed))
            {
                return false;
            }

            utc = parsed.UtcDateTime;
            return true;
        }

        private static string RemoveStripHtml(string strHtml)
        {
            return Regex.Replace(strHtml, @"<[^>]+>", string.Empty);
        }

        private static string GetValidName(string name)
        {
            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

            return Regex.Replace(name, $"[{Regex.Escape(invalidChars)}]", string.Empty);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
