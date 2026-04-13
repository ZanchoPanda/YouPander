using System;
using System.Collections.Generic;
using System.Text;
using YouPander.Models;
using YouPander.Services;

namespace YouPander.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly SettingsService _settingsService;

        public AppSettings Settings { get; set; }

        public Command SaveCommand { get; }

        public SettingsViewModel()
        {
            _settingsService = new SettingsService();
            Settings = _settingsService.Load();

            SaveCommand = new Command(async () => await Save());
        }

        private async Task Save()
        {
            _settingsService.Save(Settings);

            LocalizationResourceManager.Instance.SetCulture(Settings.Language);

            ApplyTheme();
            await Shell.Current.GoToAsync("//MainPage");
        }

        private void ApplyTheme()
        {
            var app = Application.Current;
            if (app == null || Settings == null)
                return;

            if (!string.IsNullOrEmpty(Settings.ThemeColor))
            {
                try
                {
                    var hex = Settings.ThemeColor;

                    if (!hex.StartsWith("#"))
                        hex = "#" + hex;

                    var color = Color.FromArgb(hex);

                    //app.Resources["Primary"] = color;
                    if (app.Resources["Primary"] is SolidColorBrush brush)
                    {
                        brush.Color = color;
                    }
                    else
                    {
                        app.Resources["Primary"] = new SolidColorBrush(color);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Color error: {ex.Message}");
                }
            }

            app.UserAppTheme =
                Settings.DarkMode ? AppTheme.Dark : AppTheme.Light;
        }
    }
}
