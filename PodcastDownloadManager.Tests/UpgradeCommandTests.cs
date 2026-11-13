using System;
using System.IO;
using PodcastDownloadManager.Commands;
using PodcastDownloadManager.Tests.Support;
using Xunit;

namespace PodcastDownloadManager.Tests
{
    public class UpgradeCommandTests : IDisposable
    {
        private readonly TestWorkspace _workspace = new TestWorkspace();

        public void Dispose()
        {
            _workspace.Dispose();
        }

        [Fact]
        public void Needs_update_when_the_download_list_does_not_exist()
        {
            Assert.True(UpgradeCommand.NeedsUpdate(
                Path.Combine(_workspace.Root, "NewlyReleaseInfo.txt"), DateTime.UtcNow));
        }

        [Fact]
        public void Does_not_need_update_for_a_fresh_download_list()
        {
            string path = Path.Combine(_workspace.Root, "NewlyReleaseInfo.txt");
            File.WriteAllText(path, "http://example.test/1.mp3\n");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-1));

            Assert.False(UpgradeCommand.NeedsUpdate(path, DateTime.UtcNow));
        }

        [Fact]
        public void Needs_update_for_an_old_download_list()
        {
            string path = Path.Combine(_workspace.Root, "NewlyReleaseInfo.txt");
            File.WriteAllText(path, "http://example.test/1.mp3\n");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));

            Assert.True(UpgradeCommand.NeedsUpdate(path, DateTime.UtcNow));
        }

        [Fact]
        public void Keeps_the_download_list_when_the_downloader_fails()
        {
            string downloader = CreateDownloader(7);

            if (downloader == null)
            {
                return;
            }

            ProgramConfiguration.DownloadConfigurations.DownloadProgram = DownloadTools.Aria2Name;
            ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName = downloader;
            ProgramConfiguration.SaveConfig();

            File.WriteAllText(ProgramConfiguration.PodcastNewlyReleaseInfo, "http://example.test/1.mp3\n");

            int exitCode = Program.Main(new[] { "upgrade" });

            Assert.Equal(ExitCode.Failure, exitCode);
            Assert.True(File.Exists(ProgramConfiguration.PodcastNewlyReleaseInfo));
        }

        [Fact]
        public void Deletes_the_download_list_when_the_downloader_succeeds()
        {
            string downloader = CreateDownloader(0);

            if (downloader == null)
            {
                return;
            }

            ProgramConfiguration.DownloadConfigurations.DownloadProgram = DownloadTools.Aria2Name;
            ProgramConfiguration.DownloadConfigurations.DownloadProgramPathName = downloader;
            ProgramConfiguration.SaveConfig();

            File.WriteAllText(ProgramConfiguration.PodcastNewlyReleaseInfo, "http://example.test/1.mp3\n");

            int exitCode = Program.Main(new[] { "upgrade" });

            Assert.Equal(ExitCode.Success, exitCode);
            Assert.False(File.Exists(ProgramConfiguration.PodcastNewlyReleaseInfo));
        }

        /// <summary>
        /// Creates a tiny executable that exits with <paramref name="exitCode"/>. Windows has no
        /// shell-script equivalent that Process.Start can run directly, so these tests are skipped
        /// there (CI still covers them on Ubuntu).
        /// </summary>
        private string CreateDownloader(int exitCode)
        {
            if (OperatingSystem.IsWindows())
            {
                return null;
            }

            string path = Path.Combine(_workspace.Root, "fake-downloader.sh");

            File.WriteAllText(path, $"#!/bin/sh\nexit {exitCode}\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            return path;
        }
    }
}
