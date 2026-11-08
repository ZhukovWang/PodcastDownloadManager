using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PodcastDownloadManager.Podcast;
using PodcastType = PodcastDownloadManager.Podcast.Podcast;
using PodcastDownloadManager.Tests.Support;
using Xunit;

namespace PodcastDownloadManager.Tests
{
    public class PodcastTests : IDisposable
    {
        private readonly TestWorkspace _workspace = new TestWorkspace();

        private readonly FakeFeedServer _server = new FakeFeedServer();

        public void Dispose()
        {
            _server.Dispose();
            _workspace.Dispose();
        }

        private static FeedItem Item(string title, DateTime? published, string enclosureUrl)
        {
            return new FeedItem { Title = title, PublishedUtc = published, EnclosureUrl = enclosureUrl };
        }

        private PodcastType Cached(string name, string path, string cachedFeed, TimeSpan age)
        {
            string cache = PodcastType.GetCachePath(name);
            File.WriteAllText(cache, cachedFeed);
            File.SetLastWriteTimeUtc(cache, DateTime.UtcNow - age);

            return new PodcastType(name, _server.Url(path), true);
        }

        [Fact]
        public void Collects_only_the_releases_newer_than_the_cached_feed()
        {
            _server.Serve("feed.xml", FeedXml.Rss("Show",
                Item("one day", DateTime.UtcNow.AddDays(-1), "http://example.test/1.mp3"),
                Item("two days", DateTime.UtcNow.AddDays(-2), "http://example.test/2.mp3"),
                Item("thirty days", DateTime.UtcNow.AddDays(-30), "http://example.test/3.mp3")));

            PodcastType podcast = Cached("Show", "feed.xml", "<rss><channel><title>Show</title></channel></rss>",
                TimeSpan.FromDays(7));

            FeedUpdateResult result = podcast.CollectNewlyReleases();

            Assert.True(result.Success, result.Error);
            Assert.Equal(new[] { "one day", "two days" }, result.Releases.Select(release => release.Title));
        }

        [Fact]
        public void Reports_nothing_when_the_feed_has_no_new_releases()
        {
            _server.Serve("feed.xml", FeedXml.Rss("Show",
                Item("old", DateTime.UtcNow.AddDays(-30), "http://example.test/1.mp3")));

            PodcastType podcast = Cached("Show", "feed.xml", "<rss><channel><title>Show</title></channel></rss>",
                TimeSpan.FromDays(7));

            FeedUpdateResult result = podcast.CollectNewlyReleases();

            Assert.True(result.Success, result.Error);
            Assert.Empty(result.Releases);
        }

        [Fact]
        public void Uses_the_last_update_timestamp_from_the_cached_feed()
        {
            // The feed is fetched again on every update, which rewrites the cached file. The "new since"
            // comparison must use the timestamp captured before that download.
            _server.Serve("feed.xml", FeedXml.Rss("Show",
                Item("one day", DateTime.UtcNow.AddDays(-1), "http://example.test/1.mp3")));

            PodcastType podcast = Cached("Show", "feed.xml", "<rss><channel><title>Show</title></channel></rss>",
                TimeSpan.FromDays(7));

            FeedUpdateResult first = podcast.CollectNewlyReleases();
            FeedUpdateResult second = new PodcastType("Show", _server.Url("feed.xml"), true).CollectNewlyReleases();

            Assert.Single(first.Releases);
            Assert.Empty(second.Releases);
        }

        [Fact]
        public void Keeps_the_cached_feed_when_the_download_fails()
        {
            const string cachedFeed = "<rss><channel><title>Show</title></channel></rss>";
            _server.FailWith("feed.xml", 404);

            PodcastType podcast = Cached("Show", "feed.xml", cachedFeed, TimeSpan.FromDays(7));

            FeedUpdateResult result = podcast.CollectNewlyReleases();

            Assert.False(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Error));
            Assert.Equal(cachedFeed, File.ReadAllText(PodcastType.GetCachePath("Show")));
        }

        [Fact]
        public void Reports_a_feed_without_a_channel_instead_of_throwing()
        {
            _server.Serve("feed.xml", FeedXml.RssWithoutChannel());

            PodcastType podcast = Cached("Show", "feed.xml", "<rss><channel><title>Show</title></channel></rss>",
                TimeSpan.FromDays(7));

            FeedUpdateResult result = podcast.CollectNewlyReleases();

            Assert.False(result.Success);
            Assert.Contains("channel", result.Error);
        }

        [Fact]
        public void Skips_items_without_a_pubdate_or_an_enclosure()
        {
            _server.Serve("feed.xml", FeedXml.Rss("Show",
                new FeedItem { Title = "no enclosure", PublishedUtc = DateTime.UtcNow.AddDays(-1) },
                new FeedItem { Title = "no pubdate", EnclosureUrl = "http://example.test/x.mp3" },
                new FeedItem { Title = "unreadable pubdate", PublishedUtc = null, EnclosureUrl = "http://example.test/y.mp3" },
                Item("good", DateTime.UtcNow.AddDays(-1), "http://example.test/good.mp3")));

            PodcastType podcast = Cached("Show", "feed.xml", "<rss><channel><title>Show</title></channel></rss>",
                TimeSpan.FromDays(7));

            FeedUpdateResult result = podcast.CollectNewlyReleases();

            Assert.True(result.Success, result.Error);
            Assert.Single(result.Releases);
            Assert.Equal("good", result.Releases[0].Title);
        }

        [Fact]
        public void Detects_m4a_enclosures()
        {
            _server.Serve("feed.xml", FeedXml.Rss("Show",
                Item("video", DateTime.UtcNow.AddDays(-1), "http://example.test/episode.m4a?token=1")));

            PodcastType podcast = Cached("Show", "feed.xml", "<rss><channel><title>Show</title></channel></rss>",
                TimeSpan.FromDays(7));

            FeedUpdateResult result = podcast.CollectNewlyReleases();

            Assert.EndsWith(".m4a", result.Releases[0].FileName);
        }

        [Fact]
        public void Falls_back_from_author_to_itunes_author_to_the_channel_title()
        {
            _server.Serve("feed.xml", FeedXml.Rss("Show",
                new FeedItem
                {
                    Title = "with author",
                    PublishedUtc = DateTime.UtcNow.AddDays(-1),
                    EnclosureUrl = "http://example.test/1.mp3",
                    Author = "Plain Author",
                    ItunesAuthor = "Itunes Author",
                },
                new FeedItem
                {
                    Title = "with itunes author",
                    PublishedUtc = DateTime.UtcNow.AddDays(-1),
                    EnclosureUrl = "http://example.test/2.mp3",
                    ItunesAuthor = "Itunes Author",
                },
                new FeedItem
                {
                    Title = "without author",
                    PublishedUtc = DateTime.UtcNow.AddDays(-1),
                    EnclosureUrl = "http://example.test/3.mp3",
                }));

            PodcastType podcast = Cached("Show", "feed.xml", "<rss><channel><title>Show</title></channel></rss>",
                TimeSpan.FromDays(7));

            FeedUpdateResult result = podcast.CollectNewlyReleases();

            Assert.Equal("Plain Author", result.Releases[0].Author);
            Assert.Equal("Itunes Author", result.Releases[1].Author);
            Assert.Equal("Show", result.Releases[2].Author);
        }

        [Fact]
        public void Uses_null_when_the_item_has_no_image()
        {
            _server.Serve("feed.xml", FeedXml.Rss("Show",
                new FeedItem
                {
                    Title = "with image",
                    PublishedUtc = DateTime.UtcNow.AddDays(-1),
                    EnclosureUrl = "http://example.test/1.mp3",
                    ImageUrl = "http://example.test/cover.jpg",
                },
                Item("without image", DateTime.UtcNow.AddDays(-1), "http://example.test/2.mp3")));

            PodcastType podcast = Cached("Show", "feed.xml", "<rss><channel><title>Show</title></channel></rss>",
                TimeSpan.FromDays(7));

            FeedUpdateResult result = podcast.CollectNewlyReleases();

            Assert.Equal("http://example.test/cover.jpg", result.Releases[0].ImageUrl);
            Assert.Equal("null", result.Releases[1].ImageUrl);
        }

        [Fact]
        public void Strips_html_from_the_description()
        {
            _server.Serve("feed.xml", FeedXml.Rss("Show",
                new FeedItem
                {
                    Title = "episode",
                    PublishedUtc = DateTime.UtcNow.AddDays(-1),
                    EnclosureUrl = "http://example.test/1.mp3",
                    Description = "<p>shownotes <b>bold</b></p>",
                }));

            PodcastType podcast = Cached("Show", "feed.xml", "<rss><channel><title>Show</title></channel></rss>",
                TimeSpan.FromDays(7));

            FeedUpdateResult result = podcast.CollectNewlyReleases();

            Assert.Equal("shownotes bold", result.Releases[0].Description);
        }

        [Fact]
        public void Builds_the_download_file_from_the_cached_feed()
        {
            string cachedFeed = FeedXml.Rss("Show",
                Item("new episode", DateTime.UtcNow.AddDays(-1), "http://example.test/new.mp3"),
                Item("old episode", DateTime.UtcNow.AddDays(-30), "http://example.test/old.mp3"));

            PodcastType podcast = Cached("Show", "feed.xml", cachedFeed, TimeSpan.FromDays(7));

            string downloadList = Path.Combine(_workspace.DownloadDirectory, "PodcastDownload.txt");
            FileStream stream = File.Create(downloadList);

            try
            {
                podcast.BuildPodcastDownloadFile(DateTime.UtcNow.AddDays(-7), _workspace.DownloadDirectory, false,
                    DownloadTools.Aria2Name, ref stream);
            }
            finally
            {
                stream.Close();
            }

            string text = File.ReadAllText(downloadList);

            Assert.Contains("http://example.test/new.mp3", text);
            Assert.DoesNotContain("http://example.test/old.mp3", text);
            Assert.Contains($"\tdir={_workspace.DownloadDirectory}", text);
        }

        [Fact]
        public void Writes_only_urls_for_a_simple_download_file()
        {
            string cachedFeed = FeedXml.Rss("Show",
                Item("new episode", DateTime.UtcNow.AddDays(-1), "http://example.test/new.mp3"));

            PodcastType podcast = Cached("Show", "feed.xml", cachedFeed, TimeSpan.FromDays(7));

            string downloadList = Path.Combine(_workspace.DownloadDirectory, "PodcastDownload.txt");
            FileStream stream = File.Create(downloadList);

            try
            {
                podcast.BuildPodcastDownloadFile(DateTime.UtcNow.AddDays(-7), _workspace.DownloadDirectory, true,
                    DownloadTools.Aria2Name, ref stream);
            }
            finally
            {
                stream.Close();
            }

            Assert.Equal("http://example.test/new.mp3\n", File.ReadAllText(downloadList));
        }
    }
}
