using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PodcastDownloadManager;

/// <summary>
/// Downloads the podcast feed (RSS/XML) files.
/// <para>
/// Feeds are small, but they are the only thing this tool fetches itself and it fetches them
/// often, so the details that matter here are a real User-Agent (a lot of CDNs answer 403
/// without one), transparent gzip/brotli decoding, a hard timeout covering the body as well as
/// the headers, a couple of retries on transient failures, and writing straight to disk instead
/// of buffering the whole body in memory.
/// </para>
/// </summary>
public static class DownloadHelper
{
    private const int BufferSize = 64 * 1024;

    private static readonly HttpClient HttpClient = CreateHttpClient();

    /// <summary>
    /// Attempts configured for one download, clamped to a sane range so a malformed config file
    /// cannot turn a retry loop into an infinite one.
    /// </summary>
    private static int GetMaxAttempts()
    {
        return Math.Clamp(ProgramConfiguration.DownloadConfigurations.FeedDownloadMaxAttempts,
            DownloadConfiguration.MinFeedDownloadMaxAttempts, DownloadConfiguration.MaxFeedDownloadMaxAttempts);
    }

    private static TimeSpan GetRequestTimeout()
    {
        return TimeSpan.FromSeconds(Math.Clamp(
            ProgramConfiguration.DownloadConfigurations.FeedRequestTimeoutSeconds,
            DownloadConfiguration.MinFeedRequestTimeoutSeconds, DownloadConfiguration.MaxFeedRequestTimeoutSeconds));
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        };

        var client = new HttpClient(handler)
        {
            // The per-attempt timeout is handled with a CancellationTokenSource instead, so that it
            // also covers reading the response body.
            Timeout = Timeout.InfiniteTimeSpan,
        };

        // Keep a real, descriptive User-Agent (a lot of CDNs answer 403 without one), but do not
        // put the project URL in it. feed.xyzfm.space (小宇宙) has a UA blacklist that rejects
        // any User-Agent containing "github.com" with 403 "denied by UA ACL = blacklist".
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PodcastDownloadManager/1.0 (RSS reader)");
        client.DefaultRequestHeaders.Accept.ParseAdd(
            "application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.9, */*;q=0.5");

        return client;
    }

    /// <summary>
    /// Downloads <paramref name="uri"/> to <paramref name="outputPath"/>.
    /// The file only appears at <paramref name="outputPath"/> once the download fully succeeded,
    /// so a failed transfer can never destroy a copy that was still usable.
    /// </summary>
    public static async Task DownloadFileAsync(string uri, string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri uriResult))
            throw new InvalidOperationException($"URI is invalid: {uri}");

        string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string partPath = outputPath + ".part";

        int maxAttempts = GetMaxAttempts();
        TimeSpan requestTimeout = GetRequestTimeout();

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await DownloadOnceAsync(uriResult, partPath, requestTimeout, cancellationToken).ConfigureAwait(false);

                File.Move(partPath, outputPath, true);
                return;
            }
            catch (Exception e) when (attempt < maxAttempts && IsTransient(e, cancellationToken))
            {
                Logger.Log.Warn(
                    $"Download of \"{uri}\" failed (attempt {attempt}/{maxAttempts}): {e.Message}. Retrying.");

                await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteFile(partPath);
            }
        }
    }

    private static async Task DownloadOnceAsync(Uri uri, string partPath, TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);

        using HttpResponseMessage response = await HttpClient
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Server returned {(int)response.StatusCode} {response.ReasonPhrase}.", null, response.StatusCode);
        }

        await using (Stream source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false))
        await using (var destination = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None,
                         BufferSize, useAsync: true))
        {
            await source.CopyToAsync(destination, BufferSize, timeout.Token).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken)
    {
        // A cancellation requested by the caller is a decision, not a transient failure.
        if (cancellationToken.IsCancellationRequested)
            return false;

        return exception switch
        {
            HttpRequestException http => http.StatusCode is null
                                         || http.StatusCode == HttpStatusCode.RequestTimeout
                                         || http.StatusCode == HttpStatusCode.TooManyRequests
                                         || (int)http.StatusCode >= 500,
            // Any cancellation that did not come from the caller is a timeout. HttpClient reports
            // it as TaskCanceledException today, but match the base type so another place in the
            // pipeline (or a future runtime) surfacing a plain OperationCanceledException is still
            // retried. Caller cancellation was already filtered out above.
            OperationCanceledException => true,
            IOException => true,
            _ => false,
        };
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
}
