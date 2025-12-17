using MAXTV.Services;

namespace MAXTV.Pages;

public partial class LivePage : ContentPage
{
    private readonly XtreamService _xtreamService;
    private readonly FavoritesService _favoritesService;
    private List<XtreamStream> _allStreamsCache = new(); // Cache for search/filter if needed

    public LivePage(XtreamService xtreamService, FavoritesService favoritesService)
    {
        InitializeComponent();
        _xtreamService = xtreamService;
        _favoritesService = favoritesService;
        Loaded += OnLoaded;
    }

    // Default constructor for previewer or if DI fails (fallback)
    public LivePage() : this(new XtreamService(), new FavoritesService()) { }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await LoadCategories();
    }

    private async Task LoadCategories()
    {
        LoadingSpinner.IsRunning = true;
        var categories = await _xtreamService.GetLiveCategoriesAsync();
        LoadingSpinner.IsRunning = false;

        if (categories != null)
        {
            // Inject Favorites Category
            categories.Insert(0, new XtreamCategory { CategoryId = "-1", CategoryName = " [ Favorites ] " });

            BindableLayout.SetItemsSource(CategoriesLayout, categories);

            // Force focus
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100);
                var firstButton = CategoriesLayout.Children.FirstOrDefault() as View;
                firstButton?.Focus();
            });
        }
    }

    private async void OnCategoryClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.BindingContext is XtreamCategory category)
        {
            LoadingSpinner.IsRunning = true;
            BindableLayout.SetItemsSource(ChannelsLayout, null);
            
            List<XtreamStream> streams;

            if (category.CategoryId == "-1") // Favorites
            {
                // We need to fetch all streams or check if we have them cached.
                // For a simple implementation without a database, we might have to fetch all categories 
                // OR we accept that we only show Favorites if we already visited the category? 
                // Actually, Xtream doesn't have a "Get All Streams" endpoint that is fast.
                // A better approach for Favorites in a simple app: 
                // We can't easily get *details* of a favorite channel if we don't have them loaded.
                // WORKAROUND: For now, we will just show an empty list or cached list. 
                // BUT, to make it functional: We can store the *Name* in the favorite too?
                // Let's rely on the user having visited categories, OR fetching streams is fast?
                // Real Xtream: "get_live_streams" usually returns ALL if no cat_id is passed?
                // Let's try fetching ALL streams to filter (heavy) or just assume empty for now?
                // Better: "get_live_streams" usually returns full list. Let's try it.
                streams = await _xtreamService.GetLiveStreamsAsync(""); // Empty often returns all
                if (streams != null)
                {
                    var favIds = _favoritesService.GetFavorites("live");
                    streams = streams.Where(s => favIds.Contains(s.StreamId.ToString())).ToList();
                }
                else
                {
                    streams = new List<XtreamStream>();
                }
            }
            else
            {
                streams = await _xtreamService.GetLiveStreamsAsync(category.CategoryId);
            }
            
            LoadingSpinner.IsRunning = false;
            BindableLayout.SetItemsSource(ChannelsLayout, streams);
        }
    }

    private async void OnChannelClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.BindingContext is XtreamStream stream)
        {
            string action = await DisplayActionSheet(stream.Name, "Cancel", null, "Play", "Toggle Favorite");

            if (action == "Play")
            {
                var url = _xtreamService.GetStreamUrl(stream.StreamId);
                PlayVideo(url);
            }
            else if (action == "Toggle Favorite")
            {
                _favoritesService.ToggleFavorite("live", stream.StreamId.ToString());
                await DisplayAlert("Favorites", "Favorites updated!", "OK");
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
            intent.SetPackage("org.videolan.vlc"); // Force VLC
            intent.SetFlags(Android.Content.ActivityFlags.NewTask);
            
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            context.StartActivity(intent);
        }
        catch (Android.Content.ActivityNotFoundException)
        {
            // Fallback: If VLC is not found, try generic player
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
                DisplayAlert("Player Error", "VLC Player not found, and no other video player could be opened.\nPlease install VLC for Android.", "OK");
            }
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", "Could not open video player.\n" + ex.Message, "OK");
        }
#endif
    }
}
