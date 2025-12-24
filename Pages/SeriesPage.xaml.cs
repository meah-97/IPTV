using MAXTV.Services;

namespace MAXTV.Pages;

public partial class SeriesPage : ContentPage
{
    private readonly XtreamService _xtreamService;
    private readonly FavoritesService _favoritesService;

    private List<XtreamSeries> _currentSeries = new();
    private CancellationTokenSource? _searchCts;

    public SeriesPage(XtreamService xtreamService, FavoritesService favoritesService)
    {
        InitializeComponent();
        _xtreamService = xtreamService;
        _favoritesService = favoritesService;
        Loaded += OnLoaded;
    }

    public SeriesPage() : this(new XtreamService(), new FavoritesService()) { }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await LoadCategories();
    }

    // =========================
    // LOAD CATEGORIES
    // =========================
    private async Task LoadCategories()
    {
        LoadingSpinner.IsRunning = true;
        var categories = await _xtreamService.GetSeriesCategoriesAsync();
        LoadingSpinner.IsRunning = false;

        if (categories == null)
            return;

        categories.Insert(0, new XtreamCategory
        {
            CategoryId = "-1",
            CategoryName = " [ Favorites ] "
        });

        BindableLayout.SetItemsSource(CategoriesLayout, categories);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            (CategoriesLayout.Children.FirstOrDefault() as View)?.Focus();
        });
    }

    private async void OnCategoryClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not XtreamCategory category)
            return;

        await LoadSeries(category.CategoryId ?? "");
    }

    // =========================
    // LOAD SERIES
    // =========================
    private async Task LoadSeries(string categoryId)
    {
        LoadingSpinner.IsRunning = true;
        SearchEntry.Text = string.Empty;
        BindableLayout.SetItemsSource(SeriesLayout, null);

        List<XtreamSeries> series;

        if (categoryId == "-1")
        {
            series = await _xtreamService.GetSeriesAsync("") ?? new();

            var favIds = _favoritesService.GetFavorites("series") ?? new List<string>();

            series = series
                .Where(s => favIds.Contains(s.SeriesId.ToString()))
                .ToList();
        }
        else
        {
            series = await _xtreamService.GetSeriesAsync(categoryId) ?? new();
        }

        _currentSeries = series;

        LoadingSpinner.IsRunning = false;
        BindableLayout.SetItemsSource(SeriesLayout, _currentSeries);
    }

    // =========================
    // SEARCH (SAFE + DEBOUNCED)
    // =========================
    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_currentSeries == null || _currentSeries.Count == 0)
            return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(250, token);

            var query = e.NewTextValue?.Trim().ToLowerInvariant();

            List<XtreamSeries> results;

            if (string.IsNullOrEmpty(query))
            {
                results = _currentSeries;
            }
            else
            {
                results = await Task.Run(() =>
                    _currentSeries
                        .Where(s => s.Name != null &&
                                    s.Name.ToLowerInvariant().Contains(query))
                        .ToList(),
                    token);
            }

            if (!token.IsCancellationRequested)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    BindableLayout.SetItemsSource(SeriesLayout, results);
                });
            }
        }
        catch (TaskCanceledException)
        {
            // Expected during fast typing
        }
    }

    // =========================
    // SERIES ITEM CLICK
    // =========================
    private async void OnSeriesClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not XtreamSeries series)
            return;

        string action = await DisplayActionSheet(
            series.Name,
            "Cancel",
            null,
            "View Episodes",
            "Toggle Favorite");

        if (action == "View Episodes")
        {
            await Shell.Current.GoToAsync(
                $"{nameof(SeriesDetailPage)}?SeriesId={series.SeriesId}"
            );
        }
        else if (action == "Toggle Favorite")
        {
            _favoritesService.ToggleFavorite("series", series.SeriesId.ToString());
            await DisplayAlert("Favorites", "Favorites updated!", "OK");
        }
    }

    // =========================
    // FOCUS HANDLING (FIRE TV)
    // =========================
    private void OnItemFocused(object sender, FocusEventArgs e)
    {
        if (sender is Button b)
        {
            b.BackgroundColor = Color.FromArgb("#1E88E5");

            b.Dispatcher.Dispatch(() =>
            {
                var parent = b.Parent;
                while (parent != null)
                {
                    if (parent is ScrollView scroll)
                    {
                        scroll.ScrollToAsync(b, ScrollToPosition.MakeVisible, false);
                        break;
                    }
                    parent = parent.Parent;
                }
            });
        }
    }

    private void OnItemUnfocused(object sender, FocusEventArgs e)
    {
        if (sender is Button b)
        {
            b.BackgroundColor = Colors.Transparent;
        }
    }
}
