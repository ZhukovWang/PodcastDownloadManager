using System.IO;
using PodcastDownloadManager.Tests.Support;
using Xunit;

namespace PodcastDownloadManager.Tests
{
    public class ConfigurationTests : System.IDisposable
    {
        private readonly TestWorkspace _workspace = new TestWorkspace();

        public void Dispose()
        {
            _workspace.Dispose();
        }

        [Fact]
        public void Reads_a_legacy_config_without_feed_options()
        {
            File.WriteAllText(ProgramConfiguration.ConfigFilePathName,
                "{\"DownloadPodcastPath\":\"Download\",\"DownloadProgram\":\"IDM\",\"DownloadProgramPathName\":\"IDMan.exe\"}");

            ProgramConfiguration.ReadConfig();

            Assert.Equal("IDM", ProgramConfiguration.DownloadConfigurations.DownloadProgram);
            Assert.Equal(DownloadConfiguration.DefaultFeedRequestTimeoutSeconds,
                ProgramConfiguration.DownloadConfigurations.FeedRequestTimeoutSeconds);
            Assert.Equal(DownloadConfiguration.DefaultFeedDownloadMaxAttempts,
                ProgramConfiguration.DownloadConfigurations.FeedDownloadMaxAttempts);
        }

        [Fact]
        public void Feed_options_round_trip_through_the_config_file()
        {
            ProgramConfiguration.DownloadConfigurations.FeedRequestTimeoutSeconds = 45;
            ProgramConfiguration.DownloadConfigurations.FeedDownloadMaxAttempts = 5;

            ProgramConfiguration.SaveConfig();

            ProgramConfiguration.DownloadConfigurations.FeedRequestTimeoutSeconds = 0;
            ProgramConfiguration.DownloadConfigurations.FeedDownloadMaxAttempts = 0;

            ProgramConfiguration.ReadConfig();

            Assert.Equal(45, ProgramConfiguration.DownloadConfigurations.FeedRequestTimeoutSeconds);
            Assert.Equal(5, ProgramConfiguration.DownloadConfigurations.FeedDownloadMaxAttempts);
        }
    }
}
