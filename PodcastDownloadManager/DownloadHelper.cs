using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PodcastDownloadManager;

public static class DownloadHelper
{
    private static readonly HttpClient HttpClient = new HttpClient();

    public static async Task DownloadFileAsync(string uri, string outputPath)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var uriResult))
            throw new InvalidOperationException("URI is invalid.");

        byte[] fileBytes = await HttpClient.GetByteArrayAsync(uri);

        File.WriteAllBytes(outputPath, fileBytes);
    }
}