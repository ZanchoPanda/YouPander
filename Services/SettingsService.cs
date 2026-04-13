using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using YouPander.Models;

namespace YouPander.Services
{
    public class SettingsService
    {
        private readonly string _filePath;

        public SettingsService()
        {
            _filePath = Path.Combine(FileSystem.AppDataDirectory, "settings.json");
        }

        public AppSettings Load()
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings
                {
                    DownloadPath = FileSystem.AppDataDirectory,
                    Language = "en",
                    ThemeColor = "#C34B1B",
                    DarkMode = true
                };
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json)!;
        }

        public void Save(AppSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_filePath, json);
        }
    }
}
