using System;
using System.IO;
using NFlags;
using PodcastDownloadManager.FileMetadata;
using PodcastDownloadManager.Tests.Support;
using Xunit;

namespace PodcastDownloadManager.Tests
{
    public class AudioMetadataTests : IDisposable
    {
        private readonly TestWorkspace _workspace = new TestWorkspace();

        public void Dispose()
        {
            _workspace.Dispose();
        }

        [Fact]
        public void Writes_one_value_per_line()
        {
            string path = Path.Combine(_workspace.DownloadDirectory, "episode.metadata");

            AudioMetadata.CreateAudioMetadata(path, "Album Name", "Episode Title", "Author", "http://example.test/c.jpg",
                "line one\nline two", new DateTime(2026, 11, 8, 10, 30, 0, DateTimeKind.Utc));

            string[] lines = File.ReadAllLines(path);

            Assert.Equal(7, lines.Length);
            Assert.Equal("Episode Title", lines[0]);
            Assert.Equal("Author", lines[1]);
            Assert.Equal("Album Name", lines[2]);
            Assert.Equal("Podcast", lines[3]);
            Assert.Equal("http://example.test/c.jpg", lines[5]);
        }

        [Fact]
        public void Flattens_newlines_in_the_comment()
        {
            string path = Path.Combine(_workspace.DownloadDirectory, "episode.metadata");

            AudioMetadata.CreateAudioMetadata(path, "Album", "Title", "Author", "null", "line one\r\nline two",
                DateTime.UtcNow);

            Assert.Equal("line oneline two", File.ReadAllLines(path)[6]);
        }

        [Fact]
        public void Auto_metadata_reports_a_missing_audio_file()
        {
            string metadataPath = Path.Combine(_workspace.DownloadDirectory, "missing.metadata");

            File.WriteAllLines(metadataPath, new[]
            {
                "Title", "Artist", "Album", "Podcast", DateTime.UtcNow.ToString("G"), "null", "comment",
            });

            var output = new RecordingOutput();
            IOutput outputRef = output;

            AudioMetadata.AutoAddMetadata(ref outputRef);

            Assert.True(output.Contains("does NOT exist"));
            Assert.True(File.Exists(metadataPath));
        }

        [Fact]
        public void Auto_metadata_finds_no_files_in_an_empty_directory()
        {
            var output = new RecordingOutput();
            IOutput outputRef = output;

            AudioMetadata.AutoAddMetadata(ref outputRef);

            Assert.Empty(output.Lines);
        }
    }
}
