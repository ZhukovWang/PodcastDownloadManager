using System.IO;
using PodcastDownloadManager.Tests.Support;
using Xunit;

namespace PodcastDownloadManager.Tests
{
    public class ExitCodeTests : System.IDisposable
    {
        private readonly TestWorkspace _workspace = new TestWorkspace();

        private readonly FakeFeedServer _server = new FakeFeedServer();

        public void Dispose()
        {
            _server.Dispose();
            _workspace.Dispose();
        }

        [Fact]
        public void List_returns_success_for_an_empty_library()
        {
            Assert.Equal(ExitCode.Success, Program.Main(new[] { "list" }));
        }

        [Fact]
        public void Add_returns_success_for_a_readable_feed()
        {
            _server.Serve("feed.xml", FeedXml.Rss("Show"));

            Assert.Equal(ExitCode.Success, Program.Main(new[] { "add", _server.Url("feed.xml") }));
        }

        [Fact]
        public void Add_returns_failure_for_an_invalid_url()
        {
            Assert.Equal(ExitCode.Failure, Program.Main(new[] { "add", "not-a-url" }));
        }

        [Fact]
        public void Add_returns_failure_when_the_feed_cannot_be_read()
        {
            _server.FailWith("broken.xml", 404);

            Assert.Equal(ExitCode.Failure, Program.Main(new[] { "add", _server.Url("broken.xml") }));
        }

        [Fact]
        public void Remove_returns_failure_for_an_unknown_name()
        {
            Assert.Equal(ExitCode.Failure, Program.Main(new[] { "remove", "missing" }));
        }

        [Fact]
        public void Show_returns_failure_for_an_unknown_name()
        {
            Assert.Equal(ExitCode.Failure, Program.Main(new[] { "show", "missing" }));
        }

        [Fact]
        public void Download_returns_failure_for_an_invalid_date()
        {
            Assert.Equal(ExitCode.Failure, Program.Main(new[] { "download", "not-a-date", "--simple-file" }));
        }

        [Fact]
        public void Download_select_returns_failure_for_an_unknown_name()
        {
            Assert.Equal(ExitCode.Failure, Program.Main(new[] { "download", "select", "missing", "1" }));
        }

        [Fact]
        public void Update_returns_success_for_an_empty_library()
        {
            Assert.Equal(ExitCode.Success, Program.Main(new[] { "update" }));
        }

        [Fact]
        public void Config_returns_failure_for_an_invalid_downloader()
        {
            Assert.Equal(ExitCode.Failure, Program.Main(new[] { "config", "--download-program", "Nope" }));
        }

        [Fact]
        public void Config_saves_the_feed_download_options()
        {
            string fakeDownloader = Path.Combine(_workspace.Root, "fake-downloader.exe");
            File.WriteAllText(fakeDownloader, string.Empty);

            int exitCode = Program.Main(new[]
            {
                "config",
                "--download-path", _workspace.DownloadDirectory,
                "--download-program", "Aria2",
                "--download-program-path", fakeDownloader,
                "--feed-timeout", "45",
                "--feed-retries", "5",
            });

            Assert.Equal(ExitCode.Success, exitCode);
            Assert.Equal(45, ProgramConfiguration.DownloadConfigurations.FeedRequestTimeoutSeconds);
            Assert.Equal(5, ProgramConfiguration.DownloadConfigurations.FeedDownloadMaxAttempts);
        }

        [Fact]
        public void Config_feed_options_do_not_validate_unrelated_defaults()
        {
            Directory.Delete(_workspace.DownloadDirectory, true);

            int exitCode = Program.Main(new[] { "config", "--feed-timeout", "60", "--feed-retries", "4" });

            Assert.Equal(ExitCode.Success, exitCode);
            Assert.Equal(60, ProgramConfiguration.DownloadConfigurations.FeedRequestTimeoutSeconds);
            Assert.Equal(4, ProgramConfiguration.DownloadConfigurations.FeedDownloadMaxAttempts);
        }

        [Fact]
        public void Download_simple_file_returns_success_for_an_empty_library()
        {
            Assert.Equal(ExitCode.Success,
                Program.Main(new[] { "download", "20240101", "--simple-file" }));
        }

        [Fact]
        public void Upgrade_returns_success_for_an_empty_library()
        {
            Assert.Equal(ExitCode.Success, Program.Main(new[] { "upgrade" }));
        }

        [Fact]
        public void Auto_metadata_returns_success_for_an_empty_directory()
        {
            Directory.CreateDirectory(Path.Combine(_workspace.Root, "Download"));

            Assert.Equal(ExitCode.Success, Program.Main(new[] { "autometadata" }));
        }

        [Fact]
        public void Auto_metadata_returns_failure_when_the_download_directory_is_missing()
        {
            ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath =
                Path.Combine(_workspace.Root, "missing");
            ProgramConfiguration.SaveConfig();

            Assert.Equal(ExitCode.Failure, Program.Main(new[] { "autometadata" }));
        }
    }
}
