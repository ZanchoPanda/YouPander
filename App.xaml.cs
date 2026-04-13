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
            //return new Window(new AppShell());

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
                return Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;

#elif IOS
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

#elif WINDOWS
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Downloads");

#elif MACCATALYST
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Downloads");

#else
                return FileSystem.AppDataDirectory;
#endif
        }

    }
}