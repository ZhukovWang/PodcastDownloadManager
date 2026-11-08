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
    private const int MaxAttempts = 3;

    private const int BufferSize = 64 * 1024;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private static readonly HttpClient HttpClient = CreateHttpClient();

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

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "PodcastDownloadManager/1.0 (+https://github.com/ZhukovWang/PodcastDownloadManager)");
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

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await DownloadOnceAsync(uriResult, partPath, cancellationToken).ConfigureAwait(false);

                File.Move(partPath, outputPath, true);
                return;
            }
            catch (Exception e) when (attempt < MaxAttempts && IsTransient(e, cancellationToken))
            {
                Logger.Log.Warn(
                    $"Download of \"{uri}\" failed (attempt {attempt}/{MaxAttempts}): {e.Message}. Retrying.");

                await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteFile(partPath);
            }
        }
    }

    private static async Task DownloadOnceAsync(Uri uri, string partPath, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

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
            // HttpClient reports its own timeout as a cancellation.
            TaskCanceledException => true,
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
