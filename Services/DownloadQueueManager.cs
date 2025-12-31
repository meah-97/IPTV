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

    // Track active/queued downloads to allow re-attaching listeners
    // Key: "series_123" -> Current Progress Action
    private readonly ConcurrentDictionary<string, Action<double>?> _activeProgressCallbacks = new();

    // Also track state callbacks to notify new listeners of completion
    private readonly ConcurrentDictionary<string, Action<DownloadState>?> _activeStateCallbacks = new();

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
        Action<DownloadState> onStateChanged,
        Action<double>? onProgress = null)
    {
        // 1. Register callbacks immediately so we can re-attach later if needed
        _activeProgressCallbacks[key] = onProgress;
        _activeStateCallbacks[key] = onStateChanged;

        // 2. Create item
        var item = new DownloadItem
        {
            Title = title,
            Url = url,
            LocalPath = finalPath,
            Status = "Queued"
        };

        // 3. Add to UI list (must happen on UI thread if bound)
        MainThread.BeginInvokeOnMainThread(() => Downloads.Add(item));

        // 4. Enqueue the work
        Enqueue(async () =>
        {
            // --- STARTED ---
            item.Status = "Downloading";
            DownloadRegistry.SetState(key, DownloadState.Downloading);

            NotifyState(key, DownloadState.Downloading);

            string tempPath = finalPath + ".part";

            // Update both the model (for DownloadsPage) AND the caller (SeriesDetailPage)
            var progress = new Progress<double>(mbps =>
            {
                item.SpeedMbps = mbps;
                NotifyProgress(key, mbps);
            });

            try
            {
                // Perform download
                await DownloadHelper.DownloadFileAsync(url, tempPath, progress);

                // --- SUCCESS ---
                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(tempPath, finalPath);

                item.Status = "Completed";
                DownloadRegistry.SetState(key, DownloadState.Completed);

                NotifyState(key, DownloadState.Completed);
            }
            catch (Exception ex)
            {
                // --- ERROR ---
                System.Diagnostics.Debug.WriteLine($"Download Error: {ex}");
                item.Status = "Failed";
                DownloadRegistry.Clear(key); // Reset state so user can retry

                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }

                NotifyState(key, DownloadState.None);
            }
            finally
            {
                // Cleanup callbacks
                _activeProgressCallbacks.TryRemove(key, out _);
                _activeStateCallbacks.TryRemove(key, out _);
            }
        });
    }

    /// <summary>
    /// Allows a new UI instance (e.g., after navigation) to subscribe to an ongoing download.
    /// </summary>
    public bool AttachListener(string key, Action<DownloadState> onStateChanged, Action<double> onProgress)
    {
        if (_activeProgressCallbacks.ContainsKey(key))
        {
            // Replace the old callbacks with the new ones
            _activeProgressCallbacks[key] = onProgress;
            _activeStateCallbacks[key] = onStateChanged;
            return true;
        }
        return false;
    }

    private void NotifyState(string key, DownloadState state)
    {
        if (_activeStateCallbacks.TryGetValue(key, out var callback) && callback != null)
        {
            MainThread.BeginInvokeOnMainThread(() => callback(state));
        }
    }

    private void NotifyProgress(string key, double mbps)
    {
        if (_activeProgressCallbacks.TryGetValue(key, out var callback) && callback != null)
        {
            MainThread.BeginInvokeOnMainThread(() => callback(mbps));
        }
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
