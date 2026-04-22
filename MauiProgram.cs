using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using YouPander.Handlers;
using YouPander.Services;
using YouPander.ViewModels;
using YouPander.Views;

namespace YouPander
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            var lang = Preferences.Get("Language", "en");
            LocalizationService.SetLanguage(lang);

            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
                    handlers.AddHandler<WebView, AdBlockWebViewHandler>();
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddSingleton<HistoryService>();

            // ViewModels
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<HistoryViewModel>();

            // Páginas
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<HistoryPage>();

            builder.Services.AddTransient<BrowserPage>();

#if WINDOWS

            builder.Services.AddSingleton<YtDlpService>(sp =>
            {
                var ytPath = Path.Combine(FileSystem.AppDataDirectory, "yt-dlp.exe");
                return new YtDlpService(ytPath);
            });

#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
