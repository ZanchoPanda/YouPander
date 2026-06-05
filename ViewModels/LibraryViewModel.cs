using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using YouPander.Models;

namespace YouPander.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        // ── Listas ────────────────────────────────────────────────────────
        public ObservableCollection<MediaItem> MusicItems { get; } = new();
        public ObservableCollection<MediaItem> VideoItems { get; } = new();
        public ObservableCollection<MediaItem> AllItems { get; } = new();

        [ObservableProperty] private bool _showTabs;
        [ObservableProperty] private int _selectedTabIndex;

        // ── Reproductor ───────────────────────────────────────────────────
        [ObservableProperty] private MediaItem? _currentItem;
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private bool _isShuffleOn;
        [ObservableProperty] private bool _isRepeatOn;
        [ObservableProperty] private double _volume = 1.0;
        [ObservableProperty] private double _position;
        [ObservableProperty] private double _duration;
        [ObservableProperty] private string _positionLabel = "0:00";
        [ObservableProperty] private string _durationLabel = "0:00";

        private List<MediaItem> _queue = new();
        private int _queueIndex = -1;

        [RelayCommand]
        public void SelectTab(string index) => SelectedTabIndex = int.TryParse(index, out int i) ? i : 0;

        // ── Carga desde configuración ─────────────────────────────────────
        public void LoadFromSettings(AppSettings settings)
        {
            MusicItems.Clear();
            VideoItems.Clear();
            AllItems.Clear();

            if (!settings.AdvancedConfig)
            {
                // Ruta única: una sola pestaña con todo
                ShowTabs = false;
                ScanInto(AllItems, settings.DownloadPath);
                return;
            }

            var audioPath = settings.AudioDownloadPath;
            var videoPath = settings.VideoDownloadPath;

            bool sameOrBothEmpty =
                string.IsNullOrWhiteSpace(audioPath) && string.IsNullOrWhiteSpace(videoPath) ||
                string.Equals(audioPath?.Trim(), videoPath?.Trim(), StringComparison.OrdinalIgnoreCase);

            if (sameOrBothEmpty)
            {
                ShowTabs = false;
                ScanInto(AllItems, audioPath ?? videoPath ?? settings.DownloadPath);
            }
            else
            {
                ShowTabs = true;
                ScanInto(MusicItems, audioPath);
                ScanInto(VideoItems, videoPath);
            }
        }

        private static void ScanInto(ObservableCollection<MediaItem> target, string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            {
                var item = MediaItem.TryCreate(file);
                if (item is not null)
                {
                    target.Add(item);
                }
            }
        }

        // ── Comandos de reproducción ──────────────────────────────────────
        [RelayCommand]
        public void PlayItem(MediaItem item)
        {
            var source = ShowTabs
                ? (SelectedTabIndex == 0 ? MusicItems : VideoItems)
                : AllItems;

            _queue = IsShuffleOn
                ? source.OrderBy(_ => Random.Shared.Next()).ToList()
                : source.ToList();

            _queueIndex = _queue.IndexOf(item);
            SetCurrentItem(item);
        }

        [RelayCommand]
        public void PlayPause() => IsPlaying = !IsPlaying;

        [RelayCommand]
        public void Next()
        {
            if (_queue.Count == 0)
            {
                return;
            }
            _queueIndex = (_queueIndex + 1) % _queue.Count;
            SetCurrentItem(_queue[_queueIndex]);
        }

        [RelayCommand]
        public void Previous()
        {
            if (_queue.Count == 0) return;
            _queueIndex = (_queueIndex - 1 + _queue.Count) % _queue.Count;
            SetCurrentItem(_queue[_queueIndex]);
        }

        [RelayCommand]
        public void ToggleShuffle()
        {
            IsShuffleOn = !IsShuffleOn;

            if (_queue.Count == 0) return;

            var current = _queueIndex >= 0 ? _queue[_queueIndex] : null;

            _queue = IsShuffleOn
                ? _queue.OrderBy(_ => Random.Shared.Next()).ToList()
                : (ShowTabs
                    ? (SelectedTabIndex == 0 ? MusicItems : VideoItems)
                    : AllItems).ToList();

            // Reposiciona el índice en la nueva cola
            if (current is not null)
                _queueIndex = _queue.IndexOf(current);
        }

        [RelayCommand]
        public void ToggleRepeat() => IsRepeatOn = !IsRepeatOn;

        private void SetCurrentItem(MediaItem item)
        {
            CurrentItem = item;
            IsPlaying = true;
        }

        // Llamar desde code-behind cuando MediaElement notifica progreso
        public void UpdateProgress(double positionSeconds, double durationSeconds)
        {
            Position = positionSeconds;
            Duration = durationSeconds > 0 ? durationSeconds : 1;
            PositionLabel = FormatTime(positionSeconds);
            DurationLabel = FormatTime(durationSeconds);
        }

        // Llamar desde code-behind cuando termina el medio
        public void OnMediaEnded()
        {
            if (!IsRepeatOn)
                Next();
        }

        private static string FormatTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds < 0 ? 0 : seconds);
            return t.TotalHours >= 1
                ? t.ToString(@"h\:mm\:ss")
                : t.ToString(@"m\:ss");
        }
    }
}
