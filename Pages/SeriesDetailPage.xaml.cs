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
        // Update states when returning to the page
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
                // Update states immediately after loading items
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
    // FIXED LOOP LOGIC & INTEGRATED MANAGER
    // ===============================================

    private void UpdateDownloadButtonsForEpisodes()
    {
        foreach (var view in EpisodesLayout.Children)
        {
            // Fix: The item template is a Grid, not a Button.
            if (view is Grid grid)
            {
                // Find the download button (it's the second button, or use Text/x:Name if possible,
                // but simpler here is finding the one that says 'Download'/'Queued' etc)
                // In the user's template, it's the second child in Grid.Column="1"
                var downloadBtn = grid.Children.OfType<Button>().LastOrDefault();

                if (downloadBtn != null && downloadBtn.BindingContext is XtreamEpisode episode && int.TryParse(episode.Id, out int streamId))
                {
                    UpdateButtonState(downloadBtn, episode, streamId);
                }
            }
        }
    }

    private void UpdateButtonState(Button button, XtreamEpisode episode, int streamId)
    {
        string key = $"series_{streamId}";
        string ext = episode.ContainerExtension ?? "mp4";
        string finalPath = DownloadHelper.GetLocalPath("series", $"{streamId}.{ext}");
        string tempPath = finalPath + ".part";

        var state = ResolveState(key, finalPath, tempPath);

        switch (state)
        {
            case DownloadState.Completed:
                button.Text = "Play Local";
                button.IsEnabled = true;
                break;

            case DownloadState.Downloading:
                button.Text = "Downloading…";
                button.IsEnabled = false;
                break;

            case DownloadState.Queued:
                button.Text = "Queued";
                button.IsEnabled = false;
                break;

            default:
                button.Text = "Download";
                button.IsEnabled = true;
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

    private void OnEpisodeDownloadClicked(object sender, EventArgs e)
    {
        if (sender is not Button button ||
            button.BindingContext is not XtreamEpisode episode ||
            !int.TryParse(episode.Id, out int streamId))
            return;

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
            return; // Already active

        // UI Feedback immediate
        button.Text = "Queued";
        button.IsEnabled = false;

        // Start via Manager
        DownloadQueueManager.Instance.StartDownload(
            title: episode.Title ?? $"Episode {streamId}",
            url: url,
            finalPath: finalPath,
            key: key,
            onStateChanged: (newState) =>
            {
                // This callback runs on MainThread from Manager
                UpdateButtonState(button, episode, streamId);
            }
        );
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
