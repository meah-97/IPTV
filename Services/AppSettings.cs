using Microsoft.Maui.Storage;

namespace MAXTV;

public static class AppSettings
{
    private const int DefaultLive = 180000;   // 3 min
    private const int DefaultMovie = 120000;  // 2 min
    private const int DefaultSeries = 120000; // 2 min

    public static int LiveBufferMs
    {
        get => Preferences.Get(nameof(LiveBufferMs), DefaultLive);
        set => Preferences.Set(nameof(LiveBufferMs), value);
    }

    public static int MovieBufferMs
    {
        get => Preferences.Get(nameof(MovieBufferMs), DefaultMovie);
        set => Preferences.Set(nameof(MovieBufferMs), value);
    }

    public static int SeriesBufferMs
    {
        get => Preferences.Get(nameof(SeriesBufferMs), DefaultSeries);
        set => Preferences.Set(nameof(SeriesBufferMs), value);
    }
    public static bool ShowBufferDebug
    {
        get => Preferences.Get(nameof(ShowBufferDebug), false);
        set => Preferences.Set(nameof(ShowBufferDebug), value);
    }
}