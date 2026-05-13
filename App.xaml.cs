using Microsoft.Extensions.DependencyInjection;
using YouPander.Models;
using YouPander.Services;

namespace YouPander
{
    public partial class App : Application
    {
        private readonly SettingsService _settingsService;

        public App()
        {

#if WINDOWS
    var userDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YouPander",
        "WebView2Cache"
    );
    Directory.CreateDirectory(userDataFolder);
    Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);
#endif

            InitializeComponent();
            _settingsService = new SettingsService();

            // Cargar idioma guardado
            AppSettings setting = _settingsService.Load();
            string lang = setting.Language;

            if (string.IsNullOrEmpty(lang))
                lang = "en";

            if (string.IsNullOrWhiteSpace(setting.DownloadPath))
            {
                setting.DownloadPath = GetDownloadPath();
                if (!Path.Exists(setting.DownloadPath))
                {
                    Directory.CreateDirectory(setting.DownloadPath);
                }
            }

            // Aplicar idioma al iniciar la app
            LocalizationResourceManager.Instance.SetCulture(lang);
            _settingsService.Save(setting);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            if (OperatingSystem.IsWindows())
            {
                var settings = _settingsService.Load();

                // Restaurar tamaño guardado o usar un tamaño por defecto
                window.Width = settings.WindowWidth > 0 ? settings.WindowWidth : 480;
                window.Height = settings.WindowHeight > 0 ? settings.WindowHeight : 620;

                // Guardar tamaño cuando el usuario redimensiona
                window.SizeChanged += (s, e) =>
                {
                    settings.WindowWidth = window.Width;
                    settings.WindowHeight = window.Height;
                    _settingsService.Save(settings);
                };
            }

            return window;
        }

        public static string GetDownloadPath()
        {

#if ANDROID
            return Path.Combine(Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath, AppInfo.Name);

#elif IOS
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),AppInfo.Name);

#elif WINDOWS
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    AppInfo.Name);

#elif MACCATALYST
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    ,AppInfo.Name);

#else
                return FileSystem.AppDataDirectory;
#endif
        }

    }
}