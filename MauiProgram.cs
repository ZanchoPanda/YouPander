using CommunityToolkit.Maui;
using LibVLCSharp.MAUI;
using Microsoft.Extensions.Logging;
using Serilog;
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
                .UseLibVLCSharp()
                .UseMauiCommunityToolkit()
                .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)
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

            builder.Services.AddTransient<LibraryViewModel>();

            // Páginas
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<HistoryPage>();

            builder.Services.AddTransient<BrowserPage>();

            builder.Services.AddTransient<LibraryPage>();

#if WINDOWS

            builder.Services.AddSingleton<YtDlpService>(sp =>
            {
                var ytPath = Path.Combine(FileSystem.AppDataDirectory, "yt-dlp.exe");
                return new YtDlpService(ytPath);
            });

#endif

            //#if DEBUG
            //            builder.Logging.AddDebug();
            //#endif

            var logPath = Path.Combine(FileSystem.AppDataDirectory, "logs", "youpander-.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog();

            return builder.Build();
        }
    }
}
