using System;
using System.IO;
using System.Threading.Tasks;
using PodcastDownloadManager.Tests.Support;
using Xunit;

namespace PodcastDownloadManager.Tests
{
    public class DownloadHelperTests : IDisposable
    {
        private readonly TestWorkspace _workspace = new TestWorkspace();

        private readonly FakeFeedServer _server = new FakeFeedServer();

        public void Dispose()
        {
            _server.Dispose();
            _workspace.Dispose();
        }

        [Fact]
        public async Task Downloads_the_body_to_the_target_file()
        {
            _server.Serve("feed.xml", "<rss>hello</rss>");

            string target = Path.Combine(_workspace.Root, "feed.xml");

            await DownloadHelper.DownloadFileAsync(_server.Url("feed.xml"), target,
                TestContext.Current.CancellationToken);

            Assert.Equal("<rss>hello</rss>",
                await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(target + ".part"));
        }

        [Fact]
        public async Task Creates_the_parent_directory()
        {
            _server.Serve("feed.xml", "body");

            string target = Path.Combine(_workspace.Root, "nested", "deeper", "feed.xml");

            await DownloadHelper.DownloadFileAsync(_server.Url("feed.xml"), target,
                TestContext.Current.CancellationToken);

            Assert.True(File.Exists(target));
        }

        [Fact]
        public async Task Failed_download_keeps_the_existing_file()
        {
            string target = Path.Combine(_workspace.Root, "feed.xml");
            await File.WriteAllTextAsync(target, "old", TestContext.Current.CancellationToken);

            _server.FailWith("feed.xml", 404);

            await Assert.ThrowsAsync<HttpRequestException>(
                () => DownloadHelper.DownloadFileAsync(_server.Url("feed.xml"), target,
                    TestContext.Current.CancellationToken));

            Assert.Equal("old", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(target + ".part"));
        }

        [Fact]
        public async Task Retries_a_server_error_and_then_succeeds()
        {
            _server.FailOnce("feed.xml", 500, "recovered");

            string target = Path.Combine(_workspace.Root, "feed.xml");

            await DownloadHelper.DownloadFileAsync(_server.Url("feed.xml"), target,
                TestContext.Current.CancellationToken);

            Assert.Equal("recovered", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
            Assert.Equal(2, _server.Hits("feed.xml"));
        }

        [Fact]
        public async Task Does_not_retry_a_client_error()
        {
            _server.FailWith("feed.xml", 404);

            await Assert.ThrowsAsync<HttpRequestException>(
                () => DownloadHelper.DownloadFileAsync(_server.Url("feed.xml"),
                    Path.Combine(_workspace.Root, "f.xml"), TestContext.Current.CancellationToken));

            Assert.Equal(1, _server.Hits("feed.xml"));
        }

        [Fact]
        public async Task Rejects_an_invalid_uri()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => DownloadHelper.DownloadFileAsync("not a url", Path.Combine(_workspace.Root, "f.xml"),
                    TestContext.Current.CancellationToken));
        }
    }
}
