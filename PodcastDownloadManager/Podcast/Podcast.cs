using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
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
        private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";

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
        /// Feed fields extracted on first use and reused for the lifetime of this instance. The XML
        /// is streamed and only the fields the program needs are kept, so a large feed does not stay
        /// in memory as a DOM and is parsed at most once per command.
        /// </summary>
        private ParsedFeed _feed;

        /// <summary>
        /// True when the last attempted feed download failed. The cached copy may still be usable.
        /// </summary>
        public bool DownloadFailed { get; private set; }

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
                DownloadFailed = true;
                Console.WriteLine($"Download \"{_url}\" failed.");
                Logger.Log.Error($"Download failed. File path is {_fileName}. Exception is {e}");
                return false;
            }

            Logger.Log.Info($"Download finish. File path is {_fileName}.");

            DownloadFailed = false;

            // A new file invalidates anything parsed from the previous copy.
            _feed = null;

            return true;
        }

        private string GetPodcastName()
        {
            ParsedFeed feed;
            try
            {
                feed = LoadFeed();
            }
            catch (Exception e)
            {
                Logger.Log.Error($"The feed of {_url} is not valid xml. Exception is {e}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(feed.Title))
            {
                Logger.Log.Error($"The feed of {_url} does not have a channel title.");
                return null;
            }

            return GetValidName(feed.Title.Trim().Replace(" ", ""));
        }

        public string GetPodcastDetail()
        {
            if (!FeedAvailable())
            {
                return "Error. The feed of this podcast is not available. Run 'pdlm update' first.";
            }

            ParsedFeed feed = LoadFeed();

            EnsureChannel(feed);

            string title = feed.Title ?? Name;
            string description = RemoveStripHtml(feed.Description ?? string.Empty);

            ParsedItem newlyRelease = feed.Items.FirstOrDefault();

            if (newlyRelease == null)
            {
                return $"Title: {title}\n" +
                       $"Description: {description}\n\n" +
                       "Newly release: none";
            }

            string newlyReleaseTitle = newlyRelease.Title ?? string.Empty;
            string newlyReleaseDescription = RemoveStripHtml(newlyRelease.Description ?? string.Empty);
            string newlyReleaseDownloadUrl = newlyRelease.EnclosureUrl ?? "null";

            string newlyReleasePubDate = newlyRelease.PublishedUtc.HasValue
                ? newlyRelease.PublishedUtc.Value.ToString("G")
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

            // The download replaced the cached file, so do not reuse a previously parsed document.
            _feed = null;

            ParsedFeed feed;
            try
            {
                feed = LoadFeed();
            }
            catch (Exception e)
            {
                result.Error = $"The feed is not valid xml: {e.Message}";
                Logger.Log.Error($"The feed of {Name} is not valid xml. Exception is {e}");
                return result;
            }

            if (!feed.HasChannel)
            {
                result.Error = "The xml file does not have a 'channel' node.";
                return result;
            }

            string title = feed.Title ?? Name;

            foreach (ParsedItem item in feed.Items)
            {
                if (!item.PublishedUtc.HasValue)
                {
                    Logger.Log.Warn($"{Name}: an item without a readable pubDate was skipped.");
                    continue;
                }

                DateTime dt = item.PublishedUtc.Value;

                if (dt <= lastUpdateUtc)
                {
                    continue;
                }

                string newlyReleaseTitle = (item.Title ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(item.EnclosureUrl))
                {
                    Logger.Log.Warn($"{Name}: \"{newlyReleaseTitle}\" has no enclosure url, skipped.");
                    continue;
                }

                result.Releases.Add(new NewlyRelease
                {
                    PodcastName = Name,
                    PodcastTitle = title,
                    Title = newlyReleaseTitle,
                    DownloadUrl = item.EnclosureUrl,
                    FileName = BuildFileName(title, dt, newlyReleaseTitle, item.EnclosureUrl),
                    Author = item.Author
                             ?? item.ItunesAuthor
                             ?? title,
                    ImageUrl = item.ImageUrl ?? "null",
                    Description = RemoveStripHtml(item.Description ?? "null"),
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
            ParsedFeed feed = LoadFeed();

            EnsureChannel(feed);

            string title = feed.Title ?? Name;

            foreach (ParsedItem item in feed.Items)
            {
                if (!item.PublishedUtc.HasValue)
                {
                    Logger.Log.Warn($"{Name}: an item without a readable pubDate was skipped.");
                    continue;
                }

                DateTime dt = item.PublishedUtc.Value;

                if (dt <= dateTime)
                {
                    continue;
                }

                string newlyReleaseTitle = (item.Title ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(item.EnclosureUrl))
                {
                    continue;
                }

                Logger.Log.Info($"Get the newly release title is {newlyReleaseTitle}, time is {dt:G}.");

                if (isSimpleFile)
                {
                    FileTools.AddText(fs, $"{item.EnclosureUrl}\n");
                    continue;
                }

                string fileName = BuildFileName(title, dt, newlyReleaseTitle, item.EnclosureUrl);

                AppendDownloadEntry(fs, downloadProgram, downloadDirectory, item.EnclosureUrl, fileName);

                WriteMetadata(downloadDirectory, new NewlyRelease
                {
                    PodcastName = Name,
                    PodcastTitle = title,
                    Title = newlyReleaseTitle,
                    DownloadUrl = item.EnclosureUrl,
                    FileName = fileName,
                    Author = item.Author
                             ?? item.ItunesAuthor
                             ?? "None",
                    ImageUrl = item.ImageUrl ?? "null",
                    Description = RemoveStripHtml(item.Description ?? "null"),
                    PublishedUtc = dt,
                });
            }
        }

        public void GetPodcastAllReleaseDetail(ref IOutput output)
        {
            ParsedFeed feed = LoadFeed();

            EnsureChannel(feed);

            foreach (ParsedItem item in feed.Items)
            {
                string newlyReleaseTitle = (item.Title ?? string.Empty).Trim();

                string newlyReleasePubDate = item.PublishedUtc.HasValue
                    ? item.PublishedUtc.Value.ToString("G")
                    : "unknown";

                output.WriteLine($"* {newlyReleaseTitle} - {newlyReleasePubDate}");
            }
        }

        public int DownloadSelectIndexRelease(int[] selectIndex, string downloadDirectory, bool isSimpleFile,
            string downloadProgram, ref FileStream fs)
        {
            ParsedFeed feed = LoadFeed();

            EnsureChannel(feed);

            string title = feed.Title ?? Name;

            int index = 0;
            int writtenCount = 0;

            foreach (ParsedItem item in feed.Items)
            {
                index++;

                if (!selectIndex.Contains(index))
                {
                    continue;
                }

                if (!item.PublishedUtc.HasValue)
                {
                    Logger.Log.Warn($"{Name}: release {index} has no readable pubDate, skipped.");
                    continue;
                }

                DateTime dt = item.PublishedUtc.Value;
                string newlyReleaseTitle = (item.Title ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(item.EnclosureUrl))
                {
                    Logger.Log.Warn($"{Name}: release {index} has no enclosure url, skipped.");
                    continue;
                }

                Logger.Log.Info($"Get the release title is {newlyReleaseTitle}, time is {dt:G}.");

                if (isSimpleFile)
                {
                    FileTools.AddText(fs, $"{item.EnclosureUrl}\n");
                    writtenCount++;
                    continue;
                }

                string fileName = BuildFileName(title, dt, newlyReleaseTitle, item.EnclosureUrl);

                AppendDownloadEntry(fs, downloadProgram, downloadDirectory, item.EnclosureUrl, fileName);
                writtenCount++;

                WriteMetadata(downloadDirectory, new NewlyRelease
                {
                    PodcastName = Name,
                    PodcastTitle = title,
                    Title = newlyReleaseTitle,
                    DownloadUrl = item.EnclosureUrl,
                    FileName = fileName,
                    Author = item.Author
                             ?? item.ItunesAuthor
                             ?? "None",
                    ImageUrl = item.ImageUrl ?? "null",
                    Description = RemoveStripHtml(item.Description ?? "null"),
                    PublishedUtc = dt,
                });
            }

            return writtenCount;
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

        /// <summary>
        /// A feed without a channel is not a usable podcast feed; fail instead of reporting an empty
        /// success.
        /// </summary>
        private static void EnsureChannel(ParsedFeed feed)
        {
            if (!feed.HasChannel)
            {
                throw new InvalidDataException("The xml file does not have a 'channel' node.");
            }
        }

        /// <summary>
        /// Reads the cached feed once and keeps the fields the program uses. The XML is streamed
        /// element by element, so items that are never needed do not stay in memory as a DOM.
        /// </summary>
        private ParsedFeed LoadFeed()
        {
            return _feed ?? (_feed = ParseFeed());
        }

        private ParsedFeed ParseFeed()
        {
            var feed = new ParsedFeed();

            using var reader = XmlReader.Create(_fileName, FeedReaderSettings);

            bool inChannel = false;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (inChannel && reader.Depth == 1 && reader.NamespaceURI.Length == 0
                        && reader.LocalName == "channel")
                    {
                        break;
                    }

                    continue;
                }

                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (!inChannel)
                {
                    if (reader.Depth == 1 && reader.NamespaceURI.Length == 0 && reader.LocalName == "channel")
                    {
                        inChannel = true;
                        feed.HasChannel = true;
                    }

                    continue;
                }

                if (reader.Depth != 2 || reader.NamespaceURI.Length != 0)
                {
                    continue;
                }

                switch (reader.LocalName)
                {
                    case "title":
                        feed.Title = ReadElement(reader).Value;
                        break;
                    case "description":
                        feed.Description = ReadElement(reader).Value;
                        break;
                    case "item":
                        feed.Items.Add(ToParsedItem(ReadElement(reader)));
                        break;
                }
            }

            return feed;
        }

        private static XElement ReadElement(XmlReader reader)
        {
            using XmlReader subtree = reader.ReadSubtree();

            return XElement.Load(subtree);
        }

        private static ParsedItem ToParsedItem(XElement item)
        {
            DateTime? publishedUtc = null;

            if (TryParseUtc((string)item.Element("pubDate"), out DateTime dt))
            {
                publishedUtc = dt;
            }

            return new ParsedItem
            {
                Title = (string)item.Element("title"),
                Description = (string)item.Element("description"),
                Author = (string)item.Element("author"),
                ItunesAuthor = (string)item.Element(Itunes + "author"),
                ImageUrl = (string)item.Element(Itunes + "image")?.Attribute("href"),
                EnclosureUrl = (string)item.Element("enclosure")?.Attribute("url"),
                PublishedUtc = publishedUtc,
            };
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

        private sealed class ParsedFeed
        {
            public bool HasChannel;

            public string Title;

            public string Description;

            public readonly List<ParsedItem> Items = new List<ParsedItem>();
        }

        private sealed class ParsedItem
        {
            public string Title;

            public string Description;

            public string Author;

            public string ItunesAuthor;

            public string ImageUrl;

            public string EnclosureUrl;

            public DateTime? PublishedUtc;
        }
    }
}
