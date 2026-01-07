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
            // Scan base AppDataDirectory and 'downloads' subfolder
            var root = FileSystem.AppDataDirectory;
            var downloadsDir = Path.Combine(root, "downloads");

            var allFiles = new List<string>();

            if (Directory.Exists(downloadsDir))
            {
                // Recursive search in downloads folder (handles series/movies subfolders)
                allFiles.AddRange(Directory.GetFiles(downloadsDir, "*.*", SearchOption.AllDirectories));
            }

            // Also check root just in case
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

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is LocalVideoItem item)
        {
            // Clear selection so it can be re-selected
            FilesCollection.SelectedItem = null;

            await Shell.Current.GoToAsync(
                $"{nameof(VideoPlayerPage)}" +
                $"?VideoUrl={Uri.EscapeDataString(item.Path)}" +
                $"&ContentType=local");
        }
    }
}

public class LocalVideoItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime Date { get; set; }

    // Helper for UI
    public string DisplaySize => $"{Size / 1024.0 / 1024.0:F1} MB • {Date:g}";
}
