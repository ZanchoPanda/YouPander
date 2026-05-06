using CommunityToolkit.Maui.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using YouPander.Models;
using YouPander.Services;

namespace YouPander.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly SettingsService _settingsService;

        public AppSettings Settings { get; set; }

        #region Commands

        public Command SaveCommand { get; }

        public Command BrowseFolderCommand { get; }
        public Command BrowseAudioCommand { get; }
        public Command BrowseVideoCommand { get; }
        public Command CleanAudioCommand { get; }
        public Command CleanVideoCommand { get; }

        #endregion

        public SettingsViewModel()
        {
            _settingsService = new SettingsService();
            Settings = _settingsService.Load();

            SaveCommand = new Command(async () => await Save());
            BrowseFolderCommand = new Command(async () => await BrowseFolderAsync());
            BrowseAudioCommand = new Command(async () => await BrowseAudioAsync());
            BrowseVideoCommand = new Command(async () => await BrowseVideoAsync());
            CleanAudioCommand = new Command(async () => await CleanAudioAsync());
            CleanVideoCommand = new Command(async () => await CleanVideoAsync());

            if (Settings != null)
            {
                Settings.PropertyChanged += ChangeProp;
            }
        }

        private void ChangeProp(object? sender, PropertyChangedEventArgs e)
        {
            _settingsService.Save(Settings);
        }

        public SettingsViewModel(AppSettings settingService)
        {
            _settingsService = new SettingsService();
            Settings = _settingsService.Load();

            SaveCommand = new Command(async () => await Save());
            BrowseFolderCommand = new Command(async () => await BrowseFolderAsync());
            BrowseAudioCommand = new Command(async () => await BrowseAudioAsync());
            BrowseVideoCommand = new Command(async () => await BrowseVideoAsync());
            CleanAudioCommand = new Command(async () => await CleanAudioAsync());
            CleanVideoCommand = new Command(async () => await CleanVideoAsync());
        }


        #region Actions Commands

        private async Task Save()
        {
            _settingsService.Save(Settings);

            LocalizationResourceManager.Instance.SetCulture(Settings.Language);

            ApplyTheme();
            await Shell.Current.GoToAsync("//MainPage");

            // TODO: Reinicio de app x si da mucho por saco lo del idioma
            Process.Start(Environment.ProcessPath!);
            Application.Current?.Quit();

        }

        #region Buttons

        private async Task BrowseFolderAsync()
        {
            try
            {
                var result = await OpenDialog();

                if (result.IsSuccessful)
                    Settings.DownloadPath = result.Folder.Path;
            }
            catch (Exception ex)
            {
                // Opcional: mostrar error
                var ErrorMessage = ex.Message;
            }
        }

        private async Task BrowseAudioAsync()
        {
            try
            {
                var result = await OpenDialog();

                if (result.IsSuccessful)
                    Settings.AudioDownloadPath = result.Folder.Path;
            }
            catch (Exception ex)
            {
                // Opcional: mostrar error
                var ErrorMessage = ex.Message;
            }
        }

        private async Task BrowseVideoAsync()
        {
            try
            {
                var result = await OpenDialog();

                if (result.IsSuccessful)
                    Settings.VideoDownloadPath = result.Folder.Path;
            }
            catch (Exception ex)
            {
                // Opcional: mostrar error
                var ErrorMessage = ex.Message;
            }
        }

        private async Task<FolderPickerResult> OpenDialog()
        {
            return await FolderPicker.Default.PickAsync(Settings.DownloadPath, CancellationToken.None);
        }

        private async Task CleanAudioAsync()
        {
            Settings.AudioDownloadPath = string.Empty;
            await Task.CompletedTask;
        }

        private async Task CleanVideoAsync()
        {
            Settings.VideoDownloadPath = string.Empty;
            await Task.CompletedTask;
        }

        #endregion

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

        #endregion

    }
}
