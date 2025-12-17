using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MAXTV;

[Activity(
    Label = "MAXTV",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density
)]
[IntentFilter(
    new[] { Android.Content.Intent.ActionMain },
    Categories = new[]
    {
        Android.Content.Intent.CategoryLauncher,
        Android.Content.Intent.CategoryLeanbackLauncher
    }
)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // 🚫 DO NOT TOUCH FOCUS HERE
    }
}
