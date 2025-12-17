using MAXTV.Services;

namespace MAXTV.Pages;

public partial class MoviesPage : ContentPage
{
    private readonly XtreamService _xtreamService;
    private readonly FavoritesService _favoritesService;

    public MoviesPage(XtreamService xtreamService, FavoritesService favoritesService)
    {
        InitializeComponent();
        _xtreamService = xtreamService;
        _favoritesService = favoritesService;
        Loaded += OnLoaded;
    }
    
    public MoviesPage() : this(new XtreamService(), new FavoritesService()) { }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await LoadCategories();
    }

    private async Task LoadCategories()
    {
        LoadingSpinner.IsRunning = true;
        var categories = await _xtreamService.GetVodCategoriesAsync();
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
            BindableLayout.SetItemsSource(MoviesLayout, null);
            
            List<XtreamStream> movies;
            if (category.CategoryId == "-1")
            {
                // Fetch all/filter for favorites
                movies = await _xtreamService.GetVodStreamsAsync("");
                if (movies != null)
                {
                    var favIds = _favoritesService.GetFavorites("movie");
                    movies = movies.Where(m => favIds.Contains(m.StreamId.ToString())).ToList();
                }
                else
                {
                    movies = new List<XtreamStream>();
                }
            }
            else
            {
                movies = await _xtreamService.GetVodStreamsAsync(category.CategoryId);
            }
            
            LoadingSpinner.IsRunning = false;
            BindableLayout.SetItemsSource(MoviesLayout, movies);
        }
    }

    private async void OnMovieClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.BindingContext is XtreamStream movie)
        {
            string action = await DisplayActionSheet(movie.Name, "Cancel", null, "Play", "Toggle Favorite");

            if (action == "Play")
            {
                var url = _xtreamService.GetVodStreamUrl(movie.StreamId, movie.ContainerExtension ?? "mp4");
                PlayVideo(url);
            }
            else if (action == "Toggle Favorite")
            {
                _favoritesService.ToggleFavorite("movie", movie.StreamId.ToString());
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
