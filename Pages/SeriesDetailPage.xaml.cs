using MAXTV.Services;

namespace MAXTV.Pages;

[QueryProperty(nameof(SeriesId), "SeriesId")]
public partial class SeriesDetailPage : ContentPage
{
    private readonly XtreamService _xtreamService;
    private int _seriesId;
    private XtreamSeriesDetails? _seriesDetails;

    public int SeriesId
    {
        get => _seriesId;
        set
        {
            _seriesId = value;
            LoadSeriesInfo();
        }
    }

    public SeriesDetailPage(XtreamService xtreamService)
    {
        InitializeComponent();
        _xtreamService = xtreamService;
    }

    public SeriesDetailPage() : this(new XtreamService()) { }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        MainThread.BeginInvokeOnMainThread(UpdateDownloadButtonsForEpisodes);
    }

    private async void LoadSeriesInfo()
    {
        if (_seriesId == 0) return;

        LoadingSpinner.IsRunning = true;
        _seriesDetails = await _xtreamService.GetSeriesInfoAsync(_seriesId);
        LoadingSpinner.IsRunning = false;

        if (_seriesDetails?.Episodes != null && _seriesDetails.Episodes.Count > 0)
        {
            var seasons = _seriesDetails.Episodes.Keys.ToList();
            BindableLayout.SetItemsSource(SeasonsLayout, seasons);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100);
                var firstButton = SeasonsLayout.Children.FirstOrDefault() as View;
                firstButton?.Focus();
            });
        }
    }

    private void OnSeasonClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.BindingContext is string seasonKey)
        {
            if (_seriesDetails?.Episodes != null && _seriesDetails.Episodes.TryGetValue(seasonKey, out var episodes))
            {
                BindableLayout.SetItemsSource(EpisodesLayout, episodes);
                MainThread.BeginInvokeOnMainThread(UpdateDownloadButtonsForEpisodes);
            }
        }
    }

    private void OnEpisodeClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.BindingContext is XtreamEpisode episode)
        {
            if (int.TryParse(episode.Id, out int streamId))
            {
                var url = _xtreamService.GetSeriesStreamUrl(streamId, episode.ContainerExtension ?? "mp4");
                PlayVideo(url);
            }
        }
    }

    // ===============================================
    // UI UPDATES
    // ===============================================

    private void UpdateDownloadButtonsForEpisodes()
    {
        foreach (var view in EpisodesLayout.Children)
        {
            if (view is Grid grid)
            {
                var stack = grid.Children.OfType<HorizontalStackLayout>().FirstOrDefault();
                if (stack == null) continue;

                var downloadBtn = stack.Children.OfType<Button>().FirstOrDefault(b => b.Text != "✕");
                var deleteBtn = stack.Children.OfType<Button>().FirstOrDefault(b => b.Text == "✕");

                if (downloadBtn != null && downloadBtn.BindingContext is XtreamEpisode episode && int.TryParse(episode.Id, out int streamId))
                {
                    UpdateButtonState(downloadBtn, deleteBtn, episode, streamId);
                }
            }
        }
    }

    private void UpdateButtonState(Button downloadBtn, Button? deleteBtn, XtreamEpisode episode, int streamId)
    {
        string key = $"series_{streamId}";
        string ext = episode.ContainerExtension ?? "mp4";
        string finalPath = DownloadHelper.GetLocalPath("series", $"{streamId}.{ext}");
        string tempPath = finalPath + ".part";

        var state = ResolveState(key, finalPath, tempPath);

        // Re-attach listeners if downloading
        if (state == DownloadState.Downloading || state == DownloadState.Queued)
        {
            bool attached = DownloadQueueManager.Instance.AttachListener(
                key,
                onStateChanged: (newState) =>
                {
                    UpdateButtonState(downloadBtn, deleteBtn, episode, streamId);
                },
                onProgress: (mbps) =>
                {
                    if (downloadBtn.Text != "Queued")
                    {
                        downloadBtn.Text = $"Dl: {mbps:F1} MB/s";
                    }
                }
            );
        }

        switch (state)
        {
            case DownloadState.Completed:
                downloadBtn.Text = "Play Local";
                downloadBtn.BackgroundColor = Color.FromArgb("#43A047"); // Green
                downloadBtn.TextColor = Colors.White;
                downloadBtn.IsEnabled = true;
                if (deleteBtn != null) deleteBtn.IsVisible = true;
                break;

            case DownloadState.Downloading:
                downloadBtn.Text = "Downloading…";
                downloadBtn.BackgroundColor = Color.FromArgb("#F9A825"); // Dark Yellow
                downloadBtn.TextColor = Colors.White;
                // Enable button to allow clicking to cancel
                downloadBtn.IsEnabled = true;
                if (deleteBtn != null) deleteBtn.IsVisible = false;
                break;

            case DownloadState.Queued:
                downloadBtn.Text = "Queued";
                downloadBtn.BackgroundColor = Color.FromArgb("#EF6C00"); // Orange
                downloadBtn.TextColor = Colors.White;
                // Enable button to allow clicking to cancel
                downloadBtn.IsEnabled = true;
                if (deleteBtn != null) deleteBtn.IsVisible = false;
                break;

            default:
                downloadBtn.Text = "Download";
                downloadBtn.BackgroundColor = Color.FromArgb("#1E88E5"); // Blue
                downloadBtn.TextColor = Colors.White;
                downloadBtn.IsEnabled = true;
                if (deleteBtn != null) deleteBtn.IsVisible = false;
                break;
        }
    }

    private DownloadState ResolveState(string key, string finalPath, string tempPath)
    {
        if (File.Exists(finalPath)) return DownloadState.Completed;
        if (File.Exists(tempPath)) return DownloadRegistry.GetState(key) == DownloadState.None ? DownloadState.None : DownloadState.Downloading; 
        return DownloadRegistry.GetState(key);
    }

    // ===============================================
    // HANDLERS
    // ===============================================

    private async void OnEpisodeDownloadClicked(object sender, EventArgs e)
    {
        if (sender is not Button button ||
            button.BindingContext is not XtreamEpisode episode ||
            !int.TryParse(episode.Id, out int streamId))
            return;

        Button? deleteBtn = null;
        if (button.Parent is HorizontalStackLayout stack)
        {
            deleteBtn = stack.Children.OfType<Button>().FirstOrDefault(b => b.Text == "✕");
        }

        string key = $"series_{streamId}";
        string ext = episode.ContainerExtension ?? "mp4";
        string fileName = $"{streamId}.{ext}";
        string finalPath = DownloadHelper.GetLocalPath("series", fileName);
        string tempPath = finalPath + ".part";
        string url = _xtreamService.GetSeriesStreamUrl(streamId, ext);

        var currentState = ResolveState(key, finalPath, tempPath);

        // 1. If Completed -> Play
        if (currentState == DownloadState.Completed)
        {
            await PlayLocal(finalPath);
            return;
        }

        // 2. If Downloading or Queued -> Confirm Cancel
        if (currentState == DownloadState.Downloading || currentState == DownloadState.Queued)
        {
            bool cancel = await DisplayAlert("Stop Download", "Do you want to stop this download?", "Yes", "No");
            if (cancel)
            {
                DownloadQueueManager.Instance.CancelDownload(key);
                // UI update will happen via callback/state change to None
            }
            return;
        }

        // 3. If None -> Start Download
        button.Text = "Queued";
        button.BackgroundColor = Color.FromArgb("#EF6C00"); // Orange
        // Keep enabled so user can cancel
        button.IsEnabled = true;
        if (deleteBtn != null) deleteBtn.IsVisible = false;

        DownloadQueueManager.Instance.StartDownload(
            title: episode.Title ?? $"Episode {streamId}",
            url: url,
            finalPath: finalPath,
            key: key,
            onStateChanged: (newState) =>
            {
                UpdateButtonState(button, deleteBtn, episode, streamId);
            },
            onProgress: (mbps) =>
            {
                if (button.Text != "Queued")
                {
                    button.Text = $"Dl: {mbps:F1} MB/s";
                }
            }
        );
    }

    private async void OnEpisodeDeleteClicked(object sender, EventArgs e)
    {
        if (sender is not Button deleteBtn ||
            deleteBtn.BindingContext is not XtreamEpisode episode ||
            !int.TryParse(episode.Id, out int streamId))
            return;

        bool answer = await DisplayAlert("Confirm Delete", "Are you sure you want to delete this episode?", "Yes", "No");
        if (!answer) return;

        Button? downloadBtn = null;
        if (deleteBtn.Parent is HorizontalStackLayout stack)
        {
            downloadBtn = stack.Children.OfType<Button>().FirstOrDefault(b => b.Text != "✕");
        }
        
        string key = $"series_{streamId}";
        string ext = episode.ContainerExtension ?? "mp4";
        string finalPath = DownloadHelper.GetLocalPath("series", $"{streamId}.{ext}");

        try
        {
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            DownloadRegistry.Clear(key);
            
            if (downloadBtn != null)
            {
                UpdateButtonState(downloadBtn, deleteBtn, episode, streamId);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Could not delete file: " + ex.Message, "OK");
        }
    }

    private async Task PlayLocal(string path)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(VideoPlayerPage)}" +
            $"?VideoUrl={Uri.EscapeDataString(path)}" +
            $"&ContentType=series");
    }

    private async Task PlayVideo(string url)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(VideoPlayerPage)}" +
            $"?VideoUrl={Uri.EscapeDataString(url)}" +
            $"&ContentType=series");
    }
}
