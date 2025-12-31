using MAXTV.Services;
using System.IO;

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

    // =========================
    // 🔑 CRITICAL FIX
    // =========================
    protected override void OnAppearing()
    {
        base.OnAppearing();

        // When returning via Back / screensaver,
        // re-evaluate button state from disk + registry
        MainThread.BeginInvokeOnMainThread(UpdateDownloadButtonsForEpisodes);
    }

    // =========================
    // LOAD SERIES
    // =========================
    private async void LoadSeriesInfo()
    {
        if (_seriesId == 0)
            return;

        LoadingSpinner.IsRunning = true;
        _seriesDetails = await _xtreamService.GetSeriesInfoAsync(_seriesId);
        LoadingSpinner.IsRunning = false;

        if (_seriesDetails?.Episodes != null && _seriesDetails.Episodes.Count > 0)
        {
            BindableLayout.SetItemsSource(SeasonsLayout, _seriesDetails.Episodes.Keys);
        }
    }

    // =========================
    // SEASON CLICK
    // =========================
    private void OnSeasonClicked(object? sender, EventArgs e)
    {
        if (sender is Button button &&
            button.BindingContext is string seasonKey &&
            _seriesDetails?.Episodes?.TryGetValue(seasonKey, out var episodes) == true)
        {
            BindableLayout.SetItemsSource(EpisodesLayout, episodes);
            MainThread.BeginInvokeOnMainThread(UpdateDownloadButtonsForEpisodes);
        }
    }

    // =========================
    // DISK-AWARE STATE
    // =========================
    private DownloadState ResolveState(string key, string finalPath, string tempPath)
    {
        if (File.Exists(finalPath))
            return DownloadState.Completed;

        if (File.Exists(tempPath))
            return DownloadState.Downloading;

        return DownloadRegistry.GetState(key);
    }

    // =========================
    // UPDATE BUTTONS
    // =========================
    private void UpdateDownloadButtonsForEpisodes()
    {
        foreach (var view in EpisodesLayout.Children)
        {
            if (view is not Button button ||
                button.BindingContext is not XtreamEpisode episode ||
                !int.TryParse(episode.Id, out int streamId))
                continue;

            string key = $"series_{streamId}";
            string ext = episode.ContainerExtension ?? "mp4";
            string finalPath = DownloadHelper.GetLocalPath("series", $"{streamId}.{ext}");
            string tempPath = finalPath + ".part";

            var state = ResolveState(key, finalPath, tempPath);

            switch (state)
            {
                case DownloadState.Completed:
                    DownloadRegistry.SetState(key, DownloadState.Completed);
                    button.Text = "Play Local";
                    button.IsEnabled = true;
                    break;

                case DownloadState.Downloading:
                    DownloadRegistry.SetState(key, DownloadState.Downloading);
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
    }

    // =========================
    // STREAM
    // =========================
    private async void OnEpisodeClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button ||
            button.BindingContext is not XtreamEpisode episode ||
            !int.TryParse(episode.Id, out int streamId))
            return;

        string url = _xtreamService.GetSeriesStreamUrl(
            streamId,
            episode.ContainerExtension ?? "mp4");

        await PlayVideo(url);
    }

    // =========================
    // DOWNLOAD / PLAY
    // =========================
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
            return;

        DownloadRegistry.SetState(key, DownloadState.Queued);
        button.Text = "Queued";
        button.IsEnabled = false;

        DownloadQueueManager.Instance.Enqueue(async () =>
        {
            DownloadRegistry.SetState(key, DownloadState.Downloading);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                button.Text = "Downloading…";
            });

            try
            {
                await DownloadHelper.DownloadFileAsync(url, tempPath);

                if (File.Exists(finalPath))
                    File.Delete(finalPath);

                File.Move(tempPath, finalPath);

                DownloadRegistry.SetState(key, DownloadState.Completed);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    button.Text = "Play Local";
                    button.IsEnabled = true;
                });
            }
            catch
            {
                DownloadRegistry.Clear(key);

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    button.Text = "Download";
                    button.IsEnabled = true;
                });
            }
        });
    }

    // =========================
    // PLAY VIDEO
    // =========================
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
