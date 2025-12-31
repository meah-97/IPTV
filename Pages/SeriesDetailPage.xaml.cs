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
            // The item template is now a Grid containing a HorizontalStackLayout in col 1
            if (view is Grid grid)
            {
                // Find buttons inside the HorizontalStackLayout (Grid.Column="1")
                var stack = grid.Children.OfType<HorizontalStackLayout>().FirstOrDefault();
                if (stack == null) continue;

                var downloadBtn = stack.Children.OfType<Button>().FirstOrDefault(b => b.Text != "✕"); // Main button
                var deleteBtn = stack.Children.OfType<Button>().FirstOrDefault(b => b.Text == "✕"); // Delete button

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

        switch (state)
        {
            case DownloadState.Completed:
                downloadBtn.Text = "Play Local";
                downloadBtn.IsEnabled = true;
                if (deleteBtn != null) deleteBtn.IsVisible = true;
                break;

            case DownloadState.Downloading:
                downloadBtn.Text = "Downloading…";
                downloadBtn.IsEnabled = false;
                if (deleteBtn != null) deleteBtn.IsVisible = false;
                break;

            case DownloadState.Queued:
                downloadBtn.Text = "Queued";
                downloadBtn.IsEnabled = false;
                if (deleteBtn != null) deleteBtn.IsVisible = false;
                break;

            default:
                downloadBtn.Text = "Download";
                downloadBtn.IsEnabled = true;
                if (deleteBtn != null) deleteBtn.IsVisible = false;
                break;
        }
    }

    private DownloadState ResolveState(string key, string finalPath, string tempPath)
    {
        if (File.Exists(finalPath)) return DownloadState.Completed;
        // If file exists but registry says nothing, check partial
        if (File.Exists(tempPath)) return DownloadRegistry.GetState(key) == DownloadState.None ? DownloadState.None : DownloadState.Downloading;

        return DownloadRegistry.GetState(key);
    }

    // ===============================================
    // HANDLERS
    // ===============================================

    private void OnEpisodeDownloadClicked(object sender, EventArgs e)
    {
        if (sender is not Button button ||
            button.BindingContext is not XtreamEpisode episode ||
            !int.TryParse(episode.Id, out int streamId))
            return;

        // Try to find the associated Delete button (in the same stack)
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

        if (currentState == DownloadState.Completed)
        {
            PlayLocal(finalPath);
            return;
        }

        if (currentState != DownloadState.None)
            return;

        // Initial UI Feedback
        button.Text = "Queued";
        button.IsEnabled = false;
        if (deleteBtn != null) deleteBtn.IsVisible = false;

        DownloadQueueManager.Instance.StartDownload(
            title: episode.Title ?? $"Episode {streamId}",
            url: url,
            finalPath: finalPath,
            key: key,
            onStateChanged: (newState) =>
            {
                // Refresh full state (handles transitions like Download -> Complete)
                UpdateButtonState(button, deleteBtn, episode, streamId);
            },
            onProgress: (mbps) =>
            {
                // Real-time speed update on the button
                if (button.Text != "Queued") // Don't overwrite if it's just queued
                {
                    button.Text = $"Dl: {mbps:F1} MB/s";
                }
            }
        );
    }

    private void OnEpisodeDeleteClicked(object sender, EventArgs e)
    {
        if (sender is not Button deleteBtn ||
            deleteBtn.BindingContext is not XtreamEpisode episode ||
            !int.TryParse(episode.Id, out int streamId))
            return;

        // Find associated download button
        Button? downloadBtn = null;
        if (deleteBtn.Parent is HorizontalStackLayout stack)
        {
            downloadBtn = stack.Children.OfType<Button>().FirstOrDefault(b => b.Text != "✕");
        }

        string key = $"series_{streamId}";
        string ext = episode.ContainerExtension ?? "mp4";
        string finalPath = DownloadHelper.GetLocalPath("series", $"{streamId}.{ext}");

        // Perform delete
        try
        {
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            DownloadRegistry.Clear(key);

            // Reset UI
            if (downloadBtn != null)
            {
                UpdateButtonState(downloadBtn, deleteBtn, episode, streamId);
            }
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", "Could not delete file: " + ex.Message, "OK");
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
