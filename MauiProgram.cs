using Microsoft.Extensions.Logging;
using MAXTV.Services;
using MAXTV.Pages;
using LibVLCSharp.MAUI;

namespace MAXTV
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseLibVLCSharp()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Register Services
            builder.Services.AddSingleton<XtreamService>();
            builder.Services.AddSingleton<FavoritesService>();

            // Register Pages
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<LivePage>();
            builder.Services.AddTransient<MoviesPage>();
            builder.Services.AddTransient<SeriesPage>();
            builder.Services.AddTransient<SeriesDetailPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<VideoPlayerPage>();

            // FIX: Ensure Buttons are focusable on Android TV
            Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("FixButtonFocus", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.Focusable = true;
                handler.PlatformView.FocusableInTouchMode = true;
#endif
            });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
