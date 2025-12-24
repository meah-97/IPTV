using MAXTV.Services;

namespace MAXTV.Pages;

public partial class LivePage : ContentPage
{
    private readonly XtreamService _xtreamService;
    private readonly FavoritesService _favoritesService;

    public LivePage()
    {
        InitializeComponent();
        _xtreamService = new XtreamService();
        _favoritesService = new FavoritesService();
        _ = LoadCategoriesAsync();
    }

    // =========================
    // LOAD CATEGORIES
    // =========================
    private async Task LoadCategoriesAsync()
    {
        CategoriesLayout.Children.Clear();
        ChannelsLayout.Children.Clear();

        var categories = await _xtreamService.GetLiveCategoriesAsync();
        if (categories == null)
            return;

        // Inject Favorites category (same pattern as MoviesPage)
        categories.Insert(0, new XtreamCategory
        {
            CategoryId = "-1",
            CategoryName = " [ Favorites ] "
        });

        foreach (var category in categories)
        {
            if (string.IsNullOrWhiteSpace(category.CategoryName))
                continue;

            // Always allow Favorites
            if (category.CategoryId != "-1")
            {
                var name = category.CategoryName;

                bool allowed =
                    name.StartsWith("US", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("CA", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("UK", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("SPORT", StringComparison.OrdinalIgnoreCase);

                if (!allowed)
                    continue;
            }

            var btn = CreateButton(category.CategoryName);

            btn.Clicked += async (_, _) =>
            {
                if (!string.IsNullOrEmpty(category.CategoryId))
                    await LoadChannelsAsync(category.CategoryId);
            };

            CategoriesLayout.Children.Add(btn);
        }
    }

    // =========================
    // LOAD CHANNELS
    // =========================
    private async Task LoadChannelsAsync(string categoryId)
    {
        ChannelsLayout.Children.Clear();

        List<XtreamStream> streams;

        if (categoryId == "-1")
        {
            // Favorites
            streams = await _xtreamService.GetLiveStreamsAsync("") ?? new List<XtreamStream>();
            var favIds = _favoritesService.GetFavorites("live");

            streams = streams
                .Where(s => favIds.Contains(s.StreamId.ToString()))
                .ToList();
        }
        else
        {
            streams = await _xtreamService.GetLiveStreamsAsync(categoryId)
                      ?? new List<XtreamStream>();
        }

        foreach (var stream in streams)
        {
            if (string.IsNullOrWhiteSpace(stream.Name))
                continue;

            var btn = CreateButton(stream.Name);

            btn.Clicked += async (_, _) =>
            {
                string action = await DisplayActionSheet(
                    stream.Name,
                    "Cancel",
                    null,
                    "Play",
                    "Toggle Favorite"
                );

                if (action == "Play")
                {
                    await PlayChannelAsync(stream);
                }
                else if (action == "Toggle Favorite")
                {
                    _favoritesService.ToggleFavorite("live", stream.StreamId.ToString());
                    await DisplayAlert("Favorites", "Favorites updated!", "OK");
                }
            };

            ChannelsLayout.Children.Add(btn);
        }
    }

    // =========================
    // BUTTON FACTORY
    // =========================
    private Button CreateButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            HeightRequest = 60,
            FontSize = 18,
            Padding = new Thickness(16, 0),
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White
        };

        btn.Focused += OnItemFocused;
        btn.Unfocused += OnItemUnfocused;

        return btn;
    }

    // =========================
    // FOCUS HANDLERS
    // =========================
    private void OnItemFocused(object sender, FocusEventArgs e)
    {
        if (sender is Button b)
        {
            b.BackgroundColor = Color.FromArgb("#1E88E5");
            b.TextColor = Colors.White;

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
            b.TextColor = Colors.White;
        }
    }

    // =========================
    // PLAY CHANNEL (MATCHES MOVIES)
    // =========================
    private async Task PlayChannelAsync(XtreamStream stream)
    {
        var url = _xtreamService.GetStreamUrl(
            stream.StreamId,
            stream.ContainerExtension ?? "ts"
        );

        await Shell.Current.GoToAsync(
            $"{nameof(VideoPlayerPage)}" +
            $"?VideoUrl={Uri.EscapeDataString(url)}" +
            $"&ContentType=live"
        );
    }
}

