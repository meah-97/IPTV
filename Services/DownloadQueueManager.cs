using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using MAXTV.Models;

namespace MAXTV.Services;

/// <summary>
/// Single-download-at-a-time queue. Safe for Firestick.
/// Enqueue work; it runs sequentially.
/// </summary>
public sealed class DownloadQueueManager
{
    private static readonly DownloadQueueManager _instance = new();
    public static DownloadQueueManager Instance => _instance;

    private readonly ConcurrentQueue<Func<Task>> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly object _startLock = new();
    private bool _started;

    // Provide the Downloads list for binding
    public ObservableCollection<DownloadItem> Downloads { get; } = new();

    private DownloadQueueManager() { }

    /// <summary>Add a download task to the queue.</summary>
    public void Enqueue(Func<Task> work)
    {
        _queue.Enqueue(work);
        _signal.Release();
        EnsureWorkerStarted();
    }

    /// <summary>
    /// Starts a download, managing the DownloadItem state automatically.
    /// This connects the UI (DownloadsPage) with the background task.
    /// </summary>
    public void StartDownload(
        string title,
        string url,
        string finalPath,
        string key,
        Action<DownloadState> onStateChanged)
    {
        // 1. Create item
        var item = new DownloadItem
        {
            Title = title,
            Url = url,
            LocalPath = finalPath,
            Status = "Queued"
        };

        // 2. Add to UI list (must happen on UI thread if bound)
        MainThread.BeginInvokeOnMainThread(() => Downloads.Add(item));

        // 3. Enqueue the work
        Enqueue(async () =>
        {
            // --- STARTED ---
            item.Status = "Downloading";
            DownloadRegistry.SetState(key, DownloadState.Downloading);

            // Notify caller (SeriesDetail) to update its button
            MainThread.BeginInvokeOnMainThread(() => onStateChanged(DownloadState.Downloading));

            string tempPath = finalPath + ".part";
            var progress = new Progress<double>(mbps => item.SpeedMbps = mbps);

            try
            {
                // Perform download
                await DownloadHelper.DownloadFileAsync(url, tempPath, progress);

                // --- SUCCESS ---
                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(tempPath, finalPath);

                item.Status = "Completed";
                DownloadRegistry.SetState(key, DownloadState.Completed);

                MainThread.BeginInvokeOnMainThread(() => onStateChanged(DownloadState.Completed));
            }
            catch
            {
                // --- ERROR ---
                item.Status = "Failed";
                DownloadRegistry.Clear(key); // Reset state so user can retry

                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }

                MainThread.BeginInvokeOnMainThread(() => onStateChanged(DownloadState.None));
            }
        });
    }

    private void EnsureWorkerStarted()
    {
        lock (_startLock)
        {
            if (_started) return;
            _started = true;
            _ = Task.Run(WorkerLoop);
        }
    }

    private async Task WorkerLoop()
    {
        while (true)
        {
            await _signal.WaitAsync().ConfigureAwait(false);

            if (_queue.TryDequeue(out var work))
            {
                try
                {
                    await work().ConfigureAwait(false);
                }
                catch
                {
                    // swallow so queue continues; UI will show failure via caller
                }
            }
        }
    }

    public int PendingCount => _queue.Count;
}
