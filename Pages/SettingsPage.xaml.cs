using MAXTV.Services;

namespace MAXTV.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly int[] _bufferValues = { 60000, 120000, 180000, 300000 };

    public SettingsPage()
    {
        InitializeComponent();
        UpdateLabels();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateLabels();

        // Force initial focus for Android TV
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            LiveBufferButton.Focus();
        });
    }

    private void UpdateLabels()
    {
        LiveBufferButton.Text = $"{AppSettings.LiveBufferMs / 1000} seconds";
        MovieBufferButton.Text = $"{AppSettings.MovieBufferMs / 1000} seconds";
        SeriesBufferButton.Text = $"{AppSettings.SeriesBufferMs / 1000} seconds";

        // 🔹 NEW: debug toggle label
        BufferDebugButton.Text =
            AppSettings.ShowBufferDebug
                ? "Show Buffer Info: ON"
                : "Show Buffer Info: OFF";
    }

    private int NextValue(int current)
    {
        int index = Array.IndexOf(_bufferValues, current);
        if (index < 0) index = 0;
        return _bufferValues[(index + 1) % _bufferValues.Length];
    }

    private void OnLiveBufferClicked(object sender, EventArgs e)
    {
        AppSettings.LiveBufferMs = NextValue(AppSettings.LiveBufferMs);
        UpdateLabels();
    }

    private void OnMovieBufferClicked(object sender, EventArgs e)
    {
        AppSettings.MovieBufferMs = NextValue(AppSettings.MovieBufferMs);
        UpdateLabels();
    }

    private void OnSeriesBufferClicked(object sender, EventArgs e)
    {
        AppSettings.SeriesBufferMs = NextValue(AppSettings.SeriesBufferMs);
        UpdateLabels();
    }

    // 🔹 NEW: debug toggle handler
    private void OnBufferDebugClicked(object sender, EventArgs e)
    {
        AppSettings.ShowBufferDebug = !AppSettings.ShowBufferDebug;
        UpdateLabels();
    }

    private void OnButtonFocused(object sender, FocusEventArgs e)
    {
        if (sender is Button b)
            b.BackgroundColor = Colors.DarkBlue;
    }

    private void OnButtonUnfocused(object sender, FocusEventArgs e)
    {
        if (sender is Button b)
            b.BackgroundColor = Color.FromArgb("#1E2A38");
    }
}
