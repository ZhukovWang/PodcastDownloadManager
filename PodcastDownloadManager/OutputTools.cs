using System;
using NFlags;

namespace PodcastDownloadManager
{
    /// <summary>
    /// Console-aware output helpers. Errors are printed in red only for the real console; tests
    /// and other <see cref="IOutput"/> implementations receive the same text unchanged.
    /// </summary>
    public static class OutputTools
    {
        private static readonly object ColorLock = new object();

        public static void WriteError(this IOutput output, string message)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (!ReferenceEquals(output, Output.Console))
            {
                output.WriteLine(message);
                return;
            }

            WriteColored(() => output.WriteLine(message), Console.IsOutputRedirected);
        }

        public static void WriteConsoleError(string message)
        {
            WriteColored(() => Console.Error.WriteLine(message), Console.IsErrorRedirected);
        }

        private static void WriteColored(Action write, bool redirected)
        {
            if (redirected)
            {
                write();
                return;
            }

            lock (ColorLock)
            {
                ConsoleColor previous = Console.ForegroundColor;

                try
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    write();
                }
                finally
                {
                    Console.ForegroundColor = previous;
                }
            }
        }
    }
}
