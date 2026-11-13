using System;
using System.Collections.Generic;
using System.IO;
using NFlags;

namespace PodcastDownloadManager.Tests.Support
{
    /// <summary>
    /// A throwaway directory pointed at by <see cref="ProgramConfiguration"/>, so that every test
    /// runs against its own config, opml, feed cache and download directory.
    /// </summary>
    internal sealed class TestWorkspace : IDisposable
    {
        public TestWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "pdlm-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);

            DownloadDirectory = Path.Combine(Root, "Download");
            Directory.CreateDirectory(DownloadDirectory);

            ProgramConfiguration.DownloadConfigurations = new DownloadConfiguration
            {
                DownloadPodcastPath = DownloadDirectory,
                DownloadProgram = DownloadTools.Aria2Name,
                DownloadProgramPathName = "aria2c",
            };

            ProgramConfiguration.UseDirectory(Root);
            Directory.CreateDirectory(ProgramConfiguration.PodcastsFileDirectory);
        }

        public string Root { get; }

        public string DownloadDirectory { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// An <see cref="IOutput"/> that keeps what was printed.
    /// <para>
    /// NFlags' output interface mirrors <see cref="TextWriter"/>, so all of it is forwarded there.
    /// </para>
    /// </summary>
    internal sealed class RecordingOutput : IOutput
    {
        private readonly StringWriter _writer = new StringWriter();

        public IReadOnlyList<string> Lines
        {
            get
            {
                string text = _writer.ToString().Replace("\r\n", "\n");

                return text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public bool Contains(string fragment)
        {
            foreach (string line in Lines)
            {
                if (line.Contains(fragment, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void Write(bool value) => _writer.Write(value);

        public void Write(char value) => _writer.Write(value);

        public void Write(char[] buffer) => _writer.Write(buffer);

        public void Write(char[] buffer, int index, int count) => _writer.Write(buffer, index, count);

        public void Write(decimal value) => _writer.Write(value);

        public void Write(double value) => _writer.Write(value);

        public void Write(int value) => _writer.Write(value);

        public void Write(long value) => _writer.Write(value);

        public void Write(object value) => _writer.Write(value);

        public void Write(float value) => _writer.Write(value);

        public void Write(string value) => _writer.Write(value);

        public void Write(string format, params object[] arg) => _writer.Write(format, arg);

        public void Write(uint value) => _writer.Write(value);

        public void Write(ulong value) => _writer.Write(value);

        public void WriteLine() => _writer.WriteLine();

        public void WriteLine(bool value) => _writer.WriteLine(value);

        public void WriteLine(char value) => _writer.WriteLine(value);

        public void WriteLine(char[] buffer) => _writer.WriteLine(buffer);

        public void WriteLine(char[] buffer, int index, int count) => _writer.WriteLine(buffer, index, count);

        public void WriteLine(decimal value) => _writer.WriteLine(value);

        public void WriteLine(double value) => _writer.WriteLine(value);

        public void WriteLine(int value) => _writer.WriteLine(value);

        public void WriteLine(long value) => _writer.WriteLine(value);

        public void WriteLine(object value) => _writer.WriteLine(value);

        public void WriteLine(float value) => _writer.WriteLine(value);

        public void WriteLine(string value) => _writer.WriteLine(value);

        public void WriteLine(string format, params object[] arg) => _writer.WriteLine(format, arg);

        public void WriteLine(uint value) => _writer.WriteLine(value);

        public void WriteLine(ulong value) => _writer.WriteLine(value);
    }
}
