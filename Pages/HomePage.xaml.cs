using Microsoft.Maui.Dispatching;

namespace MAXTV.Pages;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
        System.Diagnostics.Debug.WriteLine(">>> HomePage constructor");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine(">>> HomePage OnAppearing");

        // FIX: Add a small delay to ensure the UI is fully loaded before focusing
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(200);
            System.Diagnostics.Debug.WriteLine(">>> Trying to focus LiveButton");
            var focused = LiveButton?.Focus();
            System.Diagnostics.Debug.WriteLine($">>> Focus result: {focused}");
        });
    }

    async void OnLiveClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(">>> Live TV clicked");
        await Shell.Current.GoToAsync(nameof(LivePage));
    }

    async void OnMoviesClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(">>> Movies clicked");
        await Shell.Current.GoToAsync(nameof(MoviesPage));
    }

    async void OnSeriesClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(">>> Series clicked");
        await Shell.Current.GoToAsync(nameof(SeriesPage));
    }

    async void OnSettingsClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(">>> Settings clicked");
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }
}
