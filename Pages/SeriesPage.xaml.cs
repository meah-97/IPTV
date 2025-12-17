using MAXTV.Services;

namespace MAXTV.Pages;

public partial class SeriesPage : ContentPage
{
    private readonly XtreamService _xtreamService;
    private readonly FavoritesService _favoritesService;

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

    private async Task LoadCategories()
    {
        LoadingSpinner.IsRunning = true;
        var categories = await _xtreamService.GetSeriesCategoriesAsync();
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
            BindableLayout.SetItemsSource(SeriesLayout, null);
            
            List<XtreamSeries> series;
            if (category.CategoryId == "-1")
            {
                series = await _xtreamService.GetSeriesAsync("");
                if (series != null)
                {
                    var favIds = _favoritesService.GetFavorites("series");
                    series = series.Where(s => favIds.Contains(s.SeriesId.ToString())).ToList();
                }
                else
                {
                    series = new List<XtreamSeries>();
                }
            }
            else
            {
                series = await _xtreamService.GetSeriesAsync(category.CategoryId);
            }
            
            LoadingSpinner.IsRunning = false;
            BindableLayout.SetItemsSource(SeriesLayout, series);
        }
    }

    private async void OnSeriesClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.BindingContext is XtreamSeries series)
        {
            string action = await DisplayActionSheet(series.Name, "Cancel", null, "View Episodes", "Toggle Favorite");

            if (action == "View Episodes")
            {
                await Shell.Current.GoToAsync($"{nameof(SeriesDetailPage)}?SeriesId={series.SeriesId}");
            }
            else if (action == "Toggle Favorite")
            {
                _favoritesService.ToggleFavorite("series", series.SeriesId.ToString());
                 await DisplayAlert("Favorites", "Favorites updated!", "OK");
            }
        }
    }
}
