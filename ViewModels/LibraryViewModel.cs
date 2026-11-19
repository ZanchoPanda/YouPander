using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YouPander.Models;

namespace YouPander.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    // ── Listas ────────────────────────────────────────────────────────
    public ObservableCollection<MediaItem> MusicItems { get; } = new();
    public ObservableCollection<MediaItem> VideoItems { get; } = new();
    public ObservableCollection<MediaItem> AllItems { get; } = new();

    [ObservableProperty] private bool _showTabs;

    [ObservableProperty]
    private bool _hasMusic;

    [ObservableProperty]
    private bool _hasVideos;

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

    // ── Tabs ──────────────────────────────────────────────────────────
    //[RelayCommand]
    //public void SelectTab(string index) => SelectedTabIndex = int.TryParse(index, out int i) ? i : 0;
    [RelayCommand]
    public void SelectTab(string index)
    {
        if (!int.TryParse(index, out int i))
        {
            return;
        }

        if (i == 0 && !HasMusic)
        {
            return;
        }

        if (i == 1 && !HasVideos)
        {
            return;
        }

        foreach (var item in MusicItems)
        {
            item.IsExpanded = false;
        }

        foreach (var item in VideoItems)
        {
            item.IsExpanded = false;
        }

        SelectedTabIndex = i;
    }

    [RelayCommand]
    public void ToggleExpanded(MediaItem item)
    {
        // Recordar si queremos abrir o cerrar el elemento pulsado.
        bool expand = !item.IsExpanded;

        // Cerrar cualquier otro elemento abierto.
        foreach (var media in MusicItems)
        {
            media.IsExpanded = false;
        }

        foreach (var media in VideoItems)
        {
            media.IsExpanded = false;
        }

        foreach (var media in AllItems)
        {
            media.IsExpanded = false;
        }

        // Abrir el elemento seleccionado si corresponde.
        item.IsExpanded = expand;
    }

    // ── Carga desde configuración ─────────────────────────────────────
    //public void LoadFromSettings(AppSettings settings)
    //{
    //    MusicItems.Clear();
    //    VideoItems.Clear();
    //    AllItems.Clear();

    //    if (!settings.AdvancedConfig)
    //    {
    //        ShowTabs = false;
    //        ScanInto(AllItems, settings.DownloadPath);
    //        return;
    //    }

    //    var audioPath = settings.AudioDownloadPath;
    //    var videoPath = settings.VideoDownloadPath;

    //    bool sameOrBothEmpty =
    //        (string.IsNullOrWhiteSpace(audioPath) && string.IsNullOrWhiteSpace(videoPath)) ||
    //        string.Equals(audioPath?.Trim(), videoPath?.Trim(), StringComparison.OrdinalIgnoreCase);

    //    if (sameOrBothEmpty)
    //    {
    //        ShowTabs = false;
    //        ScanInto(AllItems, audioPath ?? videoPath ?? settings.DownloadPath);
    //    }
    //    else
    //    {
    //        ShowTabs = true;
    //        ScanInto(MusicItems, audioPath);
    //        ScanInto(VideoItems, videoPath);

    //        HasMusic = MusicItems.Count > 0;
    //        HasVideos = VideoItems.Count > 0;

    //        ShowTabs = HasMusic || HasVideos;

    //        // Seleccionar automáticamente una pestaña que tenga contenido.
    //        if (HasMusic)
    //        {
    //            SelectedTabIndex = 0;
    //        }
    //        else if (HasVideos)
    //        {
    //            SelectedTabIndex = 1;
    //        }
    //    }
    //}

    public void LoadFromSettings(AppSettings settings)
    {
        MusicItems.Clear();
        VideoItems.Clear();
        AllItems.Clear();

        HasMusic = false;
        HasVideos = false;

        var audioPath = settings.AdvancedConfig
            ? settings.AudioDownloadPath
            : settings.DownloadPath;

        var videoPath = settings.AdvancedConfig
            ? settings.VideoDownloadPath
            : settings.DownloadPath;

        bool samePath =
            string.IsNullOrWhiteSpace(audioPath) &&
            string.IsNullOrWhiteSpace(videoPath);

        if (!samePath &&
            !string.IsNullOrWhiteSpace(audioPath) &&
            !string.IsNullOrWhiteSpace(videoPath))
        {
            samePath = string.Equals(
                Path.GetFullPath(audioPath).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(videoPath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        if (samePath)
        {
            var path = string.IsNullOrWhiteSpace(audioPath)
                ? videoPath
                : audioPath;

            ScanInto(AllItems, path);

            ShowTabs = false;
            return;
        }

        // Cargar cada tipo exclusivamente en su colección.
        ScanInto(MusicItems, audioPath, false);
        ScanInto(VideoItems, videoPath, true);

        HasMusic = MusicItems.Count > 0;
        HasVideos = VideoItems.Count > 0;

        ShowTabs = HasMusic || HasVideos;

        // Seleccionar una pestaña que realmente tenga contenido.
        if (HasMusic)
        {
            SelectedTabIndex = 0;
        }
        else if (HasVideos)
        {
            SelectedTabIndex = 1;
        }
        else
        {
            ShowTabs = false;
        }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="path"></param>
    /// <param name="isVideo"></param>
    private static void ScanInto(
        ObservableCollection<MediaItem> target,
        string? path,
        bool? isVideo = null)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Directory.Exists(path))
            return;

        foreach (var file in Directory.EnumerateFiles(
            path,
            "*.*",
            SearchOption.AllDirectories))
        {
            var item = MediaItem.TryCreate(file);

            if (item == null)
                continue;

            if (isVideo.HasValue && item.IsVideo != isVideo.Value)
                continue;

            target.Add(item);
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
        try
        {
            if (_queue.Count == 0)
            {
                return;
            }
            _queueIndex = (_queueIndex + 1) % _queue.Count;
            SetCurrentItem(_queue[_queueIndex]);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    [RelayCommand]
    public void Previous()
    {
        if (_queue.Count == 0)
        {
            return;
        }
        _queueIndex = (_queueIndex - 1 + _queue.Count) % _queue.Count;
        SetCurrentItem(_queue[_queueIndex]);
    }

    [RelayCommand]
    public void ToggleShuffle()
    {
        try
        {
            IsShuffleOn = !IsShuffleOn;

            if (_queue.Count == 0) return;

            var current = _queueIndex >= 0 ? _queue[_queueIndex] : null;

            _queue = IsShuffleOn
                ? _queue.OrderBy(_ => Random.Shared.Next()).ToList()
                : (ShowTabs
                    ? (SelectedTabIndex == 0 ? MusicItems : VideoItems)
                    : AllItems).ToList();

            if (current is not null)
            {
                _queueIndex = _queue.IndexOf(current);
            }
        }
        catch (Exception ex)
        {
            var aux = ex;
            throw;
        }
    }

    [RelayCommand]
    public void ToggleRepeat() => IsRepeatOn = !IsRepeatOn;

    private void SetCurrentItem(MediaItem item)
    {
        Position = 0;
        Duration = 1;

        PositionLabel = "0:00";
        DurationLabel = "0:00";

        IsPlaying = false;
        CurrentItem = item;
    }

    // ── Progreso ──────────────────────────────────────────────────────
    public void UpdateProgress(double positionSeconds, double durationSeconds)
    {
        Position = positionSeconds;
        Duration = durationSeconds > 0 ? durationSeconds : 1;
        PositionLabel = FormatTime(positionSeconds);
        DurationLabel = FormatTime(durationSeconds);
    }

    public void OnMediaEnded()
    {
        if (IsRepeatOn)
        {
            SetCurrentItem(_queue[_queueIndex]); // misma pista
        }
        else
        {
            Next();
        }
    }

    private static string FormatTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds < 0 ? 0 : seconds);
        return t.TotalHours >= 1
            ? t.ToString(@"h\:mm\:ss")
            : t.ToString(@"m\:ss");
    }
}