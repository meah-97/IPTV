namespace MAXTV.Services;

public static class DownloadRegistry
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, DownloadState> _states = new();

    public static void SetState(string key, DownloadState state)
    {
        lock (_lock)
        {
            _states[key] = state;
        }
    }

    public static DownloadState GetState(string key)
    {
        lock (_lock)
        {
            return _states.TryGetValue(key, out var state)
                ? state
                : DownloadState.None;
        }
    }

    public static void Clear(string key)
    {
        lock (_lock)
        {
            _states.Remove(key);
        }
    }
}

public enum DownloadState
{
    None,
    Queued,
    Downloading,
    Completed
}
