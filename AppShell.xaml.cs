namespace MAXTV;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(Pages.MoviesPage), typeof(Pages.MoviesPage));
        Routing.RegisterRoute(nameof(Pages.SeriesPage), typeof(Pages.SeriesPage));
        Routing.RegisterRoute(nameof(Pages.SeriesDetailPage), typeof(Pages.SeriesDetailPage));
        Routing.RegisterRoute(nameof(Pages.SettingsPage), typeof(Pages.SettingsPage));
        Routing.RegisterRoute(nameof(Pages.LivePage), typeof(Pages.LivePage));
    }
}
