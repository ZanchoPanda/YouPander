
using System.Text.Json;
using YouPander.Models;

namespace YouPander.Services
{
    public class SettingsService
    {
        private readonly string _filePath;

        public SettingsService()
        {
            string directory = FileSystem.AppDataDirectory;

            Directory.CreateDirectory(directory);

            _filePath = Path.Combine(directory, "settings.json");
        }

        public AppSettings Load()
        {
            if (!File.Exists(_filePath))
            {
                return CreateDefaultSettings();
            }

            try
            {
                var json = File.ReadAllText(_filePath);

                return JsonSerializer.Deserialize<AppSettings>(json)
                       ?? CreateDefaultSettings();
            }
            catch (JsonException)
            {
                return CreateDefaultSettings();
            }
            catch (IOException)
            {
                return CreateDefaultSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_filePath, json);
        }

        private static AppSettings CreateDefaultSettings()
        {
            return new AppSettings
            {
                DownloadPath = FileSystem.AppDataDirectory,
                Language = "en",
                ThemeColor = "#C34B1B",
                DarkMode = true
            };
        }
    }
}
