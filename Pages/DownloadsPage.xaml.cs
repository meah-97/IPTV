using MAXTV.Models;
using MAXTV.Services;

namespace MAXTV.Pages;

public partial class DownloadsPage : ContentPage
{
    public DownloadsPage()
    {
        InitializeComponent();
        BindingContext = DownloadQueueManager.Instance;
    }

    private async void OnPlayClicked(object sender, EventArgs e)
    {
        if (sender is Button btn &&
            btn.BindingContext is DownloadItem item &&
            File.Exists(item.LocalPath))
        {
            await Shell.Current.GoToAsync(
                $"{nameof(VideoPlayerPage)}" +
                $"?VideoUrl={Uri.EscapeDataString(item.LocalPath)}" +
                $"&ContentType=series");
        }
    }
}
