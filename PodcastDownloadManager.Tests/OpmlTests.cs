using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NFlags;
using PodcastDownloadManager.Podcast;
using PodcastType = PodcastDownloadManager.Podcast.Podcast;
using PodcastDownloadManager.Tests.Support;
using Xunit;

namespace PodcastDownloadManager.Tests
{
    public class OpmlTests : IDisposable
    {
        private readonly TestWorkspace _workspace = new TestWorkspace();

        private readonly FakeFeedServer _server = new FakeFeedServer();

        public void Dispose()
        {
            _server.Dispose();
            _workspace.Dispose();
        }

        private static FeedItem Item(string title, DateTime published, string enclosureUrl)
        {
            return new FeedItem { Title = title, PublishedUtc = published, EnclosureUrl = enclosureUrl };
        }

        private string AddFeed(string path, string title, params FeedItem[] items)
        {
            _server.Serve(path, FeedXml.Rss(title, items));

            return Opml.AddPodcast(_server.Url(path));
        }

        private void WriteOpml(params string[] nameUrlPairs)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?><opml version=\"1.0\">");
            builder.Append("<head><title>t</title></head><body><outline text=\"feeds\">");

            for (int i = 0; i + 1 < nameUrlPairs.Length; i += 2)
            {
                builder.Append($"<outline xmlUrl=\"{nameUrlPairs[i + 1]}\" text=\"{nameUrlPairs[i]}\" type=\"rss\" />");
            }

            builder.Append("</outline></body></opml>");

            File.WriteAllText(ProgramConfiguration.PodcastFileName, builder.ToString());
        }

        private void WriteCache(string name, string feed, TimeSpan age)
        {
            string cache = PodcastType.GetCachePath(name);
            File.WriteAllText(cache, feed);
            File.SetLastWriteTimeUtc(cache, DateTime.UtcNow - age);
        }

        private static IOutput AsOutput(RecordingOutput output)
        {
            return output;
        }

        [Fact]
        public void Add_stores_the_feed_title_without_spaces()
        {
            string name = AddFeed("alpha.xml", "My Great Show");

            Assert.Equal("MyGreatShow", name);

            string opml = File.ReadAllText(ProgramConfiguration.PodcastFileName);

            Assert.Contains("MyGreatShow", opml);
            Assert.Contains(_server.Url("alpha.xml"), opml);
        }

        [Fact]
        public void Add_returns_null_when_the_feed_cannot_be_read()
        {
            _server.FailWith("broken.xml", 404);

            string name = Opml.AddPodcast(_server.Url("broken.xml"));

            Assert.Null(name);
            Assert.False(File.Exists(ProgramConfiguration.PodcastFileName));
        }

        [Fact]
        public void Remove_reports_unknown_names()
        {
            AddFeed("alpha.xml", "Show");

            Assert.Equal(1, Opml.RemovePodcast("not a podcast"));
        }

        [Fact]
        public void Remove_drops_a_known_podcast()
        {
            string name = AddFeed("alpha.xml", "Show");

            Assert.Equal(0, Opml.RemovePodcast(name));

            var output = new RecordingOutput();
            IOutput outputRef = AsOutput(output);
            Opml.ListPodcast(ref outputRef);

            Assert.Empty(output.Lines);
        }

        [Fact]
        public void List_shows_every_podcast()
        {
            AddFeed("alpha.xml", "First Show");
            AddFeed("beta.xml", "Second Show");

            var output = new RecordingOutput();
            IOutput outputRef = AsOutput(output);
            Opml.ListPodcast(ref outputRef);

            Assert.Equal(2, output.Lines.Count);
            Assert.True(output.Contains("FirstShow"));
            Assert.True(output.Contains("SecondShow"));
        }

        [Fact]
        public void List_tolerates_a_name_that_appears_twice()
        {
            WriteOpml("Duplicate", _server.Url("one.xml"), "Duplicate", _server.Url("two.xml"));

            var output = new RecordingOutput();
            IOutput outputRef = AsOutput(output);

            Opml.ListPodcast(ref outputRef);

            Assert.Single(output.Lines);
            Assert.Contains(_server.Url("two.xml"), output.Lines[0]);
        }

        [Fact]
        public void Update_reports_new_releases_of_the_whole_library()
        {
            string first = AddFeed("alpha.xml", "Show A", Item("a1", DateTime.UtcNow.AddDays(-1), "http://example.test/a1.mp3"));
            string second = AddFeed("beta.xml", "Show B", Item("b1", DateTime.UtcNow.AddDays(-2), "http://example.test/b1.mp3"));

            Assert.Equal("ShowA", first);
            Assert.Equal("ShowB", second);

            // Cold cache: nothing is newer than "now", the update only fills the cache.
            var cold = new RecordingOutput();
            IOutput coldRef = AsOutput(cold);

            Assert.Equal(0, Opml.UpdateAllPodcasts(ref coldRef));

            WriteCache(first, FeedXml.Rss("Show A", Item("a1", DateTime.UtcNow.AddDays(-1), "http://example.test/a1.mp3")),
                TimeSpan.FromDays(7));
            WriteCache(second, FeedXml.Rss("Show B", Item("b1", DateTime.UtcNow.AddDays(-2), "http://example.test/b1.mp3")),
                TimeSpan.FromDays(7));

            var warm = new RecordingOutput();
            IOutput warmRef = AsOutput(warm);

            int count = Opml.UpdateAllPodcasts(ref warmRef);

            Assert.Equal(2, count);
            Assert.True(warm.Contains("ShowA"));
            Assert.True(warm.Contains("ShowB"));

            string downloadList = File.ReadAllText(ProgramConfiguration.PodcastNewlyReleaseInfo);

            Assert.Contains("http://example.test/a1.mp3", downloadList);
            Assert.Contains($"\tdir={_workspace.DownloadDirectory}", downloadList);
            Assert.Contains("\tout=", downloadList);
        }

        [Fact]
        public void Update_keeps_going_when_one_feed_fails()
        {
            WriteOpml("Good", _server.Url("good.xml"), "Bad", _server.Url("bad.xml"));

            _server.Serve("good.xml", FeedXml.Rss("Good",
                Item("episode", DateTime.UtcNow.AddDays(-1), "http://example.test/good.mp3")));
            _server.FailWith("bad.xml", 404);

            WriteCache("Good", "<rss><channel><title>Good</title></channel></rss>", TimeSpan.FromDays(7));

            var output = new RecordingOutput();
            IOutput outputRef = AsOutput(output);

            int count = Opml.UpdateAllPodcasts(ref outputRef);

            Assert.Equal(1, count);
            Assert.True(output.Contains("Failed to update Bad"));
            Assert.True(output.Contains("Good"));
        }

        [Fact]
        public void Update_reports_an_empty_library()
        {
            var output = new RecordingOutput();
            IOutput outputRef = AsOutput(output);

            Assert.Equal(0, Opml.UpdateAllPodcasts(ref outputRef));
            Assert.True(output.Contains("library is empty"));
        }

        [Fact]
        public void Show_uses_the_cached_feed_unless_refresh_is_asked_for()
        {
            string name = AddFeed("alpha.xml", "Show", Item("episode", DateTime.UtcNow.AddDays(-1), "http://example.test/1.mp3"));

            WriteCache(name, FeedXml.Rss("Show", Item("episode", DateTime.UtcNow.AddDays(-1), "http://example.test/1.mp3")),
                TimeSpan.FromDays(1));

            int afterAdd = _server.Hits("alpha.xml");

            string detail = Opml.ShowPodcastDetail(name);

            Assert.Equal(afterAdd, _server.Hits("alpha.xml"));
            Assert.Contains("Title: Show", detail);

            Opml.ShowPodcastDetail(name, true);

            Assert.Equal(afterAdd + 1, _server.Hits("alpha.xml"));
        }

        [Fact]
        public void Show_reports_an_unknown_name()
        {
            WriteOpml();

            Assert.Contains("Error", Opml.ShowPodcastDetail("missing"));
        }

        [Fact]
        public void Download_select_writes_only_the_selected_release()
        {
            FeedItem first = Item("episode one", DateTime.UtcNow.AddDays(-1), "http://example.test/one.mp3");
            FeedItem second = Item("episode two", DateTime.UtcNow.AddDays(-2), "http://example.test/two.mp3");

            string name = AddFeed("alpha.xml", "Show", first, second);

            WriteCache(name, FeedXml.Rss("Show", first, second), TimeSpan.FromDays(1));

            int result = Opml.DownloadPodcastSelectRelease(name, new[] { 2 }, _workspace.DownloadDirectory, false,
                DownloadTools.Aria2Name);

            Assert.Equal(0, result);

            string downloadList = File.ReadAllText(Path.Combine(_workspace.DownloadDirectory, "PodcastDownload.txt"));

            Assert.Contains("episode two", downloadList);
            Assert.DoesNotContain("episode one", downloadList);
        }

        [Fact]
        public void Download_select_reports_an_unknown_name()
        {
            WriteOpml();

            Assert.Equal(-1, Opml.DownloadPodcastSelectRelease("missing", new[] { 1 },
                _workspace.DownloadDirectory, false, DownloadTools.Aria2Name));
        }

        [Fact]
        public void Download_select_returns_an_error_when_the_feed_is_missing()
        {
            WriteOpml("NoFeed", _server.Url("gone.xml"));
            _server.FailWith("gone.xml", 404);

            Assert.Equal(-1, Opml.DownloadPodcastSelectRelease("NoFeed", new[] { 1 },
                _workspace.DownloadDirectory, false, DownloadTools.Aria2Name));
        }
    }
}
