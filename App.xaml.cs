using Microsoft.Extensions.DependencyInjection;
using YouPander.Services;

namespace YouPander
{
    public partial class App : Application
    {
        private readonly SettingsService _settingsService;

        public App()
        {
            InitializeComponent();
            _settingsService = new SettingsService();

            // Cargar idioma guardado
            var setting = _settingsService.Load();
            var lang = setting.Language;

            if (string.IsNullOrEmpty(lang))
                lang = "en";

            if (string.IsNullOrWhiteSpace(setting.DownloadPath))
            {
                setting.DownloadPath = GetDownloadPath();
            }

            // Aplicar idioma al iniciar la app
            LocalizationResourceManager.Instance.SetCulture(lang);

        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        public static string GetDownloadPath()
        {
            #if ANDROID
                return Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;

            #elif IOS
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            #elif WINDOWS
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");

            #elif MACCATALYST
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");

            #else
                return FileSystem.AppDataDirectory;
            #endif
        }

    }
}