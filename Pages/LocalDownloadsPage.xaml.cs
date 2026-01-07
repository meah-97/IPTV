using System.Collections.ObjectModel;

namespace MAXTV.Pages;

public partial class LocalDownloadsPage : ContentPage
{
    public ObservableCollection<LocalVideoItem> Files { get; } = new();

    public LocalDownloadsPage()
    {
        InitializeComponent();
        FilesCollection.ItemsSource = Files;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadFiles();
    }

    private void LoadFiles()
    {
        Files.Clear();

        try
        {
            var root = FileSystem.AppDataDirectory;
            var downloadsDir = Path.Combine(root, "downloads");

            var allFiles = new List<string>();

            if (Directory.Exists(downloadsDir))
            {
                allFiles.AddRange(Directory.GetFiles(downloadsDir, "*.*", SearchOption.AllDirectories));
            }

            allFiles.AddRange(Directory.GetFiles(root, "*.*", SearchOption.TopDirectoryOnly));

            var videos = allFiles
                .Where(f => IsVideoFile(f) && !f.EndsWith(".part"))
                .Distinct()
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime);

            foreach (var info in videos)
            {
                Files.Add(new LocalVideoItem
                {
                    Name = info.Name,
                    Path = info.FullName,
                    Size = info.Length,
                    Date = info.CreationTime
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error scanning files: {ex.Message}");
        }
    }

    private bool IsVideoFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".ts";
    }

    // New Handlers for Button interaction
    private async void OnItemClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is LocalVideoItem item)
        {
            await Shell.Current.GoToAsync(
                $"{nameof(VideoPlayerPage)}" +
                $"?VideoUrl={Uri.EscapeDataString(item.Path)}" +
                $"&ContentType=local");
        }
    }

    private void OnItemFocused(object sender, FocusEventArgs e)
    {
        if (sender is Button btn && btn.Parent is Grid grid)
        {
            // Find the visual frame sibling
            var frame = grid.Children.OfType<Frame>().FirstOrDefault();
            if (frame != null)
            {
                frame.BackgroundColor = Color.FromArgb("#1E88E5"); // Focus color
                frame.BorderColor = Colors.White;

                // Optional: Scroll to item
                btn.Dispatcher.Dispatch(() =>
                {
                    FilesCollection.ScrollTo(btn.BindingContext, position: ScrollToPosition.MakeVisible, animate: false);
                });
            }
        }
    }

    private void OnItemUnfocused(object sender, FocusEventArgs e)
    {
        if (sender is Button btn && btn.Parent is Grid grid)
        {
            var frame = grid.Children.OfType<Frame>().FirstOrDefault();
            if (frame != null)
            {
                frame.BackgroundColor = Color.FromArgb("#132235"); // Default color
                frame.BorderColor = Color.FromArgb("#1E88E5");
            }
        }
    }
}

public class LocalVideoItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime Date { get; set; }

    public string DisplaySize => $"{Size / 1024.0 / 1024.0:F1} MB • {Date:g}";
}
