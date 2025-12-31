namespace MAXTV.Services;

public static class DownloadHelper
{
    public static string GetDownloadsFolder(string contentType)
    {
        // App-private storage: /data/data/<pkg>/files/...
        var folder = Path.Combine(FileSystem.AppDataDirectory, "downloads", contentType);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string GetLocalPath(string contentType, string fileName)
        => Path.Combine(GetDownloadsFolder(contentType), fileName);

    /// <summary>
    /// Downloads URL to destinationPath (you should pass a .part path),
    /// calling speedProgress with MB/s updates (approx).
    /// </summary>
    public static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<double>? speedProgress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        using var http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        });

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[1024 * 128];
        long bytesSinceLast = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellationToken);
            if (read <= 0) break;

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

            bytesSinceLast += read;

            if (speedProgress != null && sw.ElapsedMilliseconds >= 700)
            {
                var mbps = (bytesSinceLast / 1024d / 1024d) / (sw.ElapsedMilliseconds / 1000d);
                speedProgress.Report(mbps);

                bytesSinceLast = 0;
                sw.Restart();
            }
        }

        // final report (optional)
        if (speedProgress != null && bytesSinceLast > 0 && sw.ElapsedMilliseconds > 0)
        {
            var mbps = (bytesSinceLast / 1024d / 1024d) / (sw.ElapsedMilliseconds / 1000d);
            speedProgress.Report(mbps);
        }
    }

    /// <summary>Deletes any leftover .part files for a content type.</summary>
    public static void CleanupPartFiles(string contentType)
    {
        var folder = GetDownloadsFolder(contentType);
        if (!Directory.Exists(folder)) return;

        foreach (var part in Directory.GetFiles(folder, "*.part"))
        {
            try { File.Delete(part); } catch { }
        }
    }
}
