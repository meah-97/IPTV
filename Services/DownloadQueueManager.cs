using System.Collections.Concurrent;

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

    private DownloadQueueManager() { }

    /// <summary>Add a download task to the queue.</summary>
    public void Enqueue(Func<Task> work)
    {
        _queue.Enqueue(work);
        _signal.Release();
        EnsureWorkerStarted();
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
