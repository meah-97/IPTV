using MAXTV.Services;

namespace MAXTV.Pages;

[QueryProperty(nameof(SeriesId), "SeriesId")]
public partial class SeriesDetailPage : ContentPage
{
    private readonly XtreamService _xtreamService;
    private int _seriesId;
    
    // SeriesId property for QueryProperty
    public int SeriesId
    {
        get => _seriesId;
        set
        {
            _seriesId = value;
            LoadSeriesInfo();
        }
    }

    private XtreamSeriesDetails? _seriesDetails;

    public SeriesDetailPage(XtreamService xtreamService)
    {
        InitializeComponent();
        _xtreamService = xtreamService;
    }
    
    public SeriesDetailPage() : this(new XtreamService()) { }

    private async void LoadSeriesInfo()
    {
        if (_seriesId == 0) return;

        LoadingSpinner.IsRunning = true;
        _seriesDetails = await _xtreamService.GetSeriesInfoAsync(_seriesId);
        LoadingSpinner.IsRunning = false;

        if (_seriesDetails?.Episodes != null && _seriesDetails.Episodes.Count > 0)
        {
            // Populate Seasons (keys of the dictionary)
            // Example keys: "1", "2", "3"
            var seasons = _seriesDetails.Episodes.Keys.ToList();
            BindableLayout.SetItemsSource(SeasonsLayout, seasons);

            // Force focus
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
            if (_seriesDetails?.Episodes != null && _seriesDetails.Episodes.ContainsKey(seasonKey))
            {
                var episodes = _seriesDetails.Episodes[seasonKey];
                BindableLayout.SetItemsSource(EpisodesLayout, episodes);
            }
        }
    }

    private void OnEpisodeClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.BindingContext is XtreamEpisode episode)
        {
            // Parse ID to int if needed by API, but Xtream often uses stream_id logic
            // Episode ID in model is string, but service expects int streamId
            if (int.TryParse(episode.Id, out int streamId))
            {
                // Use GetSeriesStreamUrl for Episodes
                var url = _xtreamService.GetSeriesStreamUrl(streamId, episode.ContainerExtension ?? "mp4");
                PlayVideo(url);
            }
        }
    }

    private void PlayVideo(string url)
    {
#if ANDROID
        try
        {
            var uri = Android.Net.Uri.Parse(url);
            var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
            intent.SetDataAndType(uri, "video/*");
            intent.SetPackage("org.videolan.vlc");
            intent.SetFlags(Android.Content.ActivityFlags.NewTask);
            
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            context.StartActivity(intent);
        }
        catch (Android.Content.ActivityNotFoundException)
        {
             try
            {
                var uri = Android.Net.Uri.Parse(url);
                var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
                intent.SetDataAndType(uri, "video/*");
                intent.SetFlags(Android.Content.ActivityFlags.NewTask);
                
                var context = Platform.CurrentActivity ?? Android.App.Application.Context;
                context.StartActivity(intent);
            }
            catch (Exception ex)
            {
                DisplayAlert("Player Error", "VLC Player not found.\nPlease install VLC for Android.", "OK");
            }
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", "Could not open video player.\n" + ex.Message, "OK");
        }
#endif
    }
}
