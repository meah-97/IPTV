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
    private readonly ConcurrentDictionary<string, Action<double>?> _activeProgressCallbacks = new();
    private readonly ConcurrentDictionary<string, Action<DownloadState>?> _activeStateCallbacks = new();
    
    // Cancellation support
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();

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
        // 1. Create cancellation source
        var cts = new CancellationTokenSource();
        _cancellationTokens[key] = cts;

        // 2. Register callbacks
        _activeProgressCallbacks[key] = onProgress;
        _activeStateCallbacks[key] = onStateChanged;

        // 3. Set Registry State to QUEUED
        DownloadRegistry.SetState(key, DownloadState.Queued);

        // 4. Create item
        var item = new DownloadItem
        {
            Title = title,
            Url = url,
            LocalPath = finalPath,
            Status = "Queued"
        };

        MainThread.BeginInvokeOnMainThread(() => Downloads.Add(item));

        // 5. Enqueue the work
        Enqueue(async () =>
        {
            // If cancelled before starting (e.g. while in queue)
            if (cts.IsCancellationRequested)
            {
                Cleanup(key, finalPath, item, DownloadState.None);
                return;
            }

            // --- STARTED ---
            item.Status = "Downloading";
            DownloadRegistry.SetState(key, DownloadState.Downloading);
            NotifyState(key, DownloadState.Downloading);

            string tempPath = finalPath + ".part";
            
            var progress = new Progress<double>(mbps => 
            {
                item.SpeedMbps = mbps;
                NotifyProgress(key, mbps);
            });

            try
            {
                // Perform download with cancellation token
                await DownloadHelper.DownloadFileAsync(url, tempPath, progress, cts.Token);

                // --- SUCCESS ---
                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(tempPath, finalPath);

                item.Status = "Completed";
                DownloadRegistry.SetState(key, DownloadState.Completed);
                NotifyState(key, DownloadState.Completed);
            }
            catch (OperationCanceledException)
            {
                // --- CANCELLED ---
                System.Diagnostics.Debug.WriteLine($"Download Cancelled: {key}");
                Cleanup(key, finalPath + ".part", item, DownloadState.None);
            }
            catch (Exception ex)
            {
                // --- ERROR ---
                System.Diagnostics.Debug.WriteLine($"Download Error: {ex}");
                Cleanup(key, finalPath + ".part", item, DownloadState.None);
            }
            finally
            {
                _activeProgressCallbacks.TryRemove(key, out _);
                _activeStateCallbacks.TryRemove(key, out _);
                _cancellationTokens.TryRemove(key, out _);
                cts.Dispose();
            }
        });
    }

    private void Cleanup(string key, string pathToDelete, DownloadItem item, DownloadState finalState)
    {
        item.Status = "Failed"; // or Cancelled
        DownloadRegistry.Clear(key);
        try { if (File.Exists(pathToDelete)) File.Delete(pathToDelete); } catch { }
        NotifyState(key, finalState);
    }

    /// <summary>
    /// Cancels an active or queued download.
    /// </summary>
    public void CancelDownload(string key)
    {
        if (_cancellationTokens.TryGetValue(key, out var cts))
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException) { }
        }
    }

    /// <summary>
    /// Allows a new UI instance (e.g., after navigation) to subscribe to an ongoing download.
    /// </summary>
    public bool AttachListener(string key, Action<DownloadState> onStateChanged, Action<double> onProgress)
    {
        if (_activeProgressCallbacks.ContainsKey(key))
        {
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
                    // swallow
                }
            }
        }
    }

    public int PendingCount => _queue.Count;
}
