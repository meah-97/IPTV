using MAXTV.Services;

namespace MAXTV.Pages;

public partial class MoviesPage : ContentPage
{
    private readonly XtreamService _xtreamService;
    private readonly FavoritesService _favoritesService;

    private List<XtreamStream> _currentMovies = new();
    private CancellationTokenSource? _searchCts;

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

        if (categories == null)
            return;

        // Inject Favorites category
        categories.Insert(0, new XtreamCategory
        {
            CategoryId = "-1",
            CategoryName = " [ Favorites ] "
        });

        BindableLayout.SetItemsSource(CategoriesLayout, categories);

        // Force initial focus (Fire TV)
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            (CategoriesLayout.Children.FirstOrDefault() as View)?.Focus();
        });
    }

    private async void OnCategoryClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not XtreamCategory category)
            return;

        LoadingSpinner.IsRunning = true;
        BindableLayout.SetItemsSource(MoviesLayout, null);
        SearchEntry.Text = string.Empty;

        List<XtreamStream> movies;

        if (category.CategoryId == "-1")
        {
            movies = await _xtreamService.GetVodStreamsAsync("") ?? new List<XtreamStream>();

            var favIds = _favoritesService.GetFavorites("movie") ?? new List<string>();

            movies = movies
                .Where(m => favIds.Contains(m.StreamId.ToString()))
                .ToList();
        }
        else
        {
            movies = await _xtreamService.GetVodStreamsAsync(category.CategoryId)
                     ?? new List<XtreamStream>();
        }

        _currentMovies = movies;

        LoadingSpinner.IsRunning = false;
        BindableLayout.SetItemsSource(MoviesLayout, _currentMovies);
    }

    private async void OnMovieClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not XtreamStream movie)
            return;

        string action = await DisplayActionSheet(
            movie.Name,
            "Cancel",
            null,
            "Play",
            "Toggle Favorite");

        if (action == "Play")
        {
            var url = _xtreamService.GetVodStreamUrl(
                movie.StreamId,
                movie.ContainerExtension ?? "mp4");

            await Shell.Current.GoToAsync(
                $"{nameof(VideoPlayerPage)}" +
                $"?VideoUrl={Uri.EscapeDataString(url)}" +
                $"&ContentType=movie");
        }
        else if (action == "Toggle Favorite")
        {
            _favoritesService.ToggleFavorite("movie", movie.StreamId.ToString());
            await DisplayAlert("Favorites", "Favorites updated!", "OK");
        }
    }

    // 🔒 SAFE, DEBOUNCED, NON-BLOCKING SEARCH
    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_currentMovies == null || _currentMovies.Count == 0)
            return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(250, token);

            var query = e.NewTextValue?.Trim().ToLowerInvariant();

            List<XtreamStream> results;

            if (string.IsNullOrEmpty(query))
            {
                results = _currentMovies;
            }
            else
            {
                results = await Task.Run(() =>
                    _currentMovies
                        .Where(m => m.Name != null &&
                                    m.Name.ToLowerInvariant().Contains(query))
                        .ToList(),
                    token);
            }

            if (!token.IsCancellationRequested)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    BindableLayout.SetItemsSource(MoviesLayout, results);
                });
            }
        }
        catch (TaskCanceledException)
        {
            // Expected during fast typing
        }
    }

    private void OnCategoryFocused(object sender, FocusEventArgs e)
    {
        if (sender is Button button)
            VisualStateManager.GoToState(button, "Focused");
    }
}
