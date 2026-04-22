using System.Collections.ObjectModel;
using System.Windows.Input;
using YouPander.Models;
using YouPander.Resources.Localization;
using YouPander.Services;
using YouPander.ViewModels;

public class MainViewModel : BaseViewModel, IQueryAttributable
{
    #region Fields

    private CancellationTokenSource? _cts;
    private readonly YtDlpService? _ytDlp;
    private readonly SettingsService _settings;
    private readonly HistoryService _history;

    #endregion

    #region Properties

    private string _url = string.Empty;
    public string Url
    {
        get => _url;
        set
        {
            if (value != _url)
            {
                _url = value;
                OnPropertyChanged(nameof(Url));
            }
        }
    }

    private string _selectedFormat = "Audio";
    public string SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (value != _selectedFormat)
            {
                _selectedFormat = value;
                OnPropertyChanged(nameof(SelectedFormat));
            }
        }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set
        {
            if (value != _progress)
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }
    }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        set
        {
            if (value != _status)
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (value != _isDownloading)
            {
                _isDownloading = value;
                OnPropertyChanged(nameof(IsDownloading));
                NotifyCommandsCanExecuteChanged();
            }
        }
    }

    private bool _isSearching;
    public bool IsSearching
    {
        get => _isSearching;
        set
        {
            if (_isSearching != value)
            {
                _isSearching = value; OnPropertyChanged();
                NotifyCommandsCanExecuteChanged();
            }
        }
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (value != _errorMessage)
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private ObservableCollection<FormatOption> _availableFormats = new();
    public ObservableCollection<FormatOption> AvailableFormats
    {
        get => _availableFormats;
        set { _availableFormats = value; OnPropertyChanged(); }
    }

    private FormatOption? _selectedFormatOption;
    public FormatOption? SelectedFormatOption
    {
        get => _selectedFormatOption;
        set { _selectedFormatOption = value; OnPropertyChanged(); }
    }

    public bool HasFormats => AvailableFormats.Count > 0;

    #region Videos

    private ObservableCollection<VideoItem> _VideoItems;

    public ObservableCollection<VideoItem> VideoItems
    {
        get
        {
            return _VideoItems;
        }
        set
        {
            if (value != _VideoItems)
            {
                _VideoItems = value;
                OnPropertyChanged("VideoItems");

            }
        }
    }

    public bool ShowVideoList => VideoItems.Count > 1;


    // Campo - ajusta el número máximo de descargas simultáneas
    private readonly SemaphoreSlim _downloadSemaphore = new(3);

    #endregion

    #endregion

    #region Commands

    public Command SearchCommand { get; }
    public Command DownloadCommand { get; }
    public Command OpenSettingsCommand { get; }
    public Command CancelCommand { get; }

    #endregion

    public MainViewModel()
    {
        _settings = new SettingsService();

        if (OperatingSystem.IsWindows())
        {
            var ytPath = Path.Combine(FileSystem.AppDataDirectory, "yt-dlp.exe");
            _ytDlp = new YtDlpService(ytPath);
        }

        VideoItems = new ObservableCollection<VideoItem>();
        VideoItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowVideoList));

        SearchCommand = new Command(async () => await SearchAsync(), () => !IsSearching && !IsDownloading);
        DownloadCommand = new Command(async () => await Download(), () => !IsDownloading);
        CancelCommand = new Command(async () => await CancelDownload(), () => IsDownloading);
        OpenSettingsCommand = new Command(async () => await Shell.Current.GoToAsync("///SettingsPage"));
    }

    public MainViewModel(SettingsService settings, HistoryService history, YtDlpService? ytDlp = null)
    {
        _settings = settings;
        _history = history;
        _ytDlp = OperatingSystem.IsWindows() ? ytDlp : null;

        VideoItems = new ObservableCollection<VideoItem>();
        VideoItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowVideoList));

        SearchCommand = new Command(async () => await SearchAsync(), () => !IsSearching && !IsDownloading);
        DownloadCommand = new Command(async () => await Download(), () => !IsDownloading);
        CancelCommand = new Command(async () => await CancelDownload(), () => IsDownloading);
        OpenSettingsCommand = new Command(async () => await Shell.Current.GoToAsync("///SettingsPage"));
    }

    #region Commands-Actions

    private async Task SearchAsync()
    {
        ErrorMessage = string.Empty;
        VideoItems.Clear();


        if (string.IsNullOrWhiteSpace(Url)) return;
        if (!OperatingSystem.IsWindows() || _ytDlp == null)
        {
            Status = Strings.OnlyAvailableOnWindows;
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsSearching = true;
        Status = Strings.FetchingInfo;

        try
        {
            await _ytDlp.EnsureInstalledAsync();
            var infos = await _ytDlp.FetchInfoAsync(Url, _cts.Token);

            foreach (var info in infos)
            {
                VideoItems.Add(new VideoItem
                {
                    Id = info.Id,
                    Title = info.Title,
                    Channel = info.Channel,
                    ThumbnailUrl = info.Thumbnail,
                    Url = info.Url,
                    Duration = info.Duration,
                    IsSelected = true
                });
            }


            NotifyCommandsCanExecuteChanged();

            // Si es un solo video, lanzar descarga directamente
            //if (VideoItems.Count == 1)
            //    await Download();

            if (VideoItems.Count == 1)
            {
                var formats = await _ytDlp.FetchFormatsAsync(Url, _cts.Token);
                AvailableFormats = new ObservableCollection<FormatOption>(formats);
                OnPropertyChanged(nameof(HasFormats));

                SelectedFormatOption = AvailableFormats.FirstOrDefault(f => f.FormatId == "mp3");
                // No lanzar descarga automática, esperar a que el usuario elija formato
                return;
            }

        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally
        {
            IsSearching = false;
            Status = string.Empty;
        }
    }

    private async Task Download()
    {
        ErrorMessage = string.Empty;
        var settings = _settings.Load();

        if (string.IsNullOrWhiteSpace(settings.DownloadPath)) return;
        if (!OperatingSystem.IsWindows() || _ytDlp == null) return;

        var toDownload = VideoItems.Count == 1
            ? VideoItems.ToList()
            : VideoItems.Where(v => v.IsSelected).ToList();

        if (!toDownload.Any()) return;

        _cts = new CancellationTokenSource();
        IsDownloading = true;

        try
        {
            await _ytDlp.EnsureFfmpegInstalledAsync();

            #region Version 1

            //foreach (VideoItem item in toDownload)
            //{
            //    if (_cts.Token.IsCancellationRequested) break;

            //    item.Status = Strings.Downloading;
            //    item.Progress = 0;

            //    var progress = new Progress<string>(line =>
            //    {
            //        MainThread.BeginInvokeOnMainThread(() =>
            //        {
            //            if (line.StartsWith("⚠️"))
            //            {
            //                item.Status = line;
            //                return;
            //            }

            //            item.Status = line;

            //            // Actualizar también el progreso global si es un solo video
            //            if (!ShowVideoList) Status = line;

            //            if (line.Contains('%'))
            //            {
            //                var parts = line.Split('%')[0].Split(' ');
            //                if (double.TryParse(parts.Last(),
            //                    System.Globalization.NumberStyles.Any,
            //                    System.Globalization.CultureInfo.InvariantCulture,
            //                    out double value))
            //                {
            //                    item.Progress = value / 100.0;
            //                    if (!ShowVideoList) Progress = item.Progress;
            //                }
            //            }
            //        });
            //    });

            //    try
            //    {
            //        await _ytDlp.DownloadAsync(item.Url, settings.DownloadPath, SelectedFormat, progress, _cts.Token, SelectedFormatOption?.FormatId);

            //        item.IsDownloaded = true;
            //        item.Progress = 1.0;
            //        item.Status = Strings.Done;

            //        #region

            //        await _history.AddAsync(new DownloadRecord
            //        {
            //            Title = item.Title,
            //            Url = item.Url,
            //            Channel = item.Channel,
            //            ThumbnailUrl = item.ThumbnailUrl,
            //            Format = SelectedFormat,
            //            DownloadPath = settings.DownloadPath,
            //            DownloadedAt = DateTime.Now,
            //            Success = true
            //        });

            //        #endregion

            //    }
            //    catch (OperationCanceledException)
            //    {
            //        item.Status = Strings.Cancelled; break;
            //    }
            //    catch (Exception ex)
            //    {
            //        item.Status = $"❌ {ex.Message}";
            //        await _history.AddAsync(new DownloadRecord
            //        {
            //            Title = item.Title,
            //            Url = item.Url,
            //            Format = SelectedFormat,
            //            DownloadedAt = DateTime.Now,
            //            Success = false
            //        });
            //    }
            //}
            #endregion

            #region Version 2

            List<Task> tasks = toDownload.Select(item => DownloadItemAsync(item, settings, _cts.Token)).ToList();
            await Task.WhenAll(tasks);

            #endregion

            if (settings.OpenDownloads && !_cts.Token.IsCancellationRequested)
                await OpenDownloads();
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!ShowVideoList) { Progress = 0; Status = string.Empty; }
                IsDownloading = false;
            });
        }

    }

    private async Task CancelDownload()
    {
        _cts?.Cancel();
        _ytDlp?.KillCurrentProcess();
        Status = $"{Strings.CancelDownloads}...";
        await Task.Delay(300);
        Status = string.Empty;
        IsSearching = false;
        NotifyCommandsCanExecuteChanged();

    }

    public async Task OpenDownloads()
    {
        var settings = _settings.Load();
        Page? page = Application.Current?.Windows?.FirstOrDefault()?.Page;

        try
        {
            await Launcher.OpenAsync(settings.DownloadPath);
        }
        catch (Exception ex)
        {
            if (page != null)
                await page.DisplayAlertAsync(Strings.Error, $"{Strings.CannotOpenFolder}: {ex.Message}", Strings.Ok);
        }
    }

    private void NotifyCommandsCanExecuteChanged()
    {
        (SearchCommand as Command)?.ChangeCanExecute();
        (DownloadCommand as Command)?.ChangeCanExecute();
        (CancelCommand as Command)?.ChangeCanExecute();
    }

    public ICommand OpenUrlCommand => new Command<string>(async (url) =>
    {
        if (!string.IsNullOrEmpty(url))
        {
            //await Launcher.Default.OpenAsync(url);
            await Shell.Current.GoToAsync($"BrowserPage?url={Uri.EscapeDataString(url)}");
        }
    });

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("url", out var url))
        {
            Url = Uri.UnescapeDataString(url.ToString() ?? string.Empty);
        }
    }

    private async Task DownloadItemAsync(VideoItem item, AppSettings settings, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        // Mostrar estado de cola ANTES de esperar el semáforo
        item.Status = "⏳ En cola...";
        item.Progress = 0;

        await _downloadSemaphore.WaitAsync(token);

        try
        {
            if (token.IsCancellationRequested)
            {
                item.Status = Strings.Cancelled;
                return;
            }

            // Ya tiene su slot — empieza la descarga real
            item.Status = Strings.Downloading;

            var progress = new Progress<string>(line =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (line.StartsWith("⚠️"))
                    {
                        item.Status = line;
                        return;
                    }

                    item.Status = line;

                    if (!ShowVideoList) Status = line;

                    if (line.Contains('%'))
                    {
                        var parts = line.Split('%')[0].Split(' ');
                        if (double.TryParse(parts.Last(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double value))
                        {
                            item.Progress = value / 100.0;
                            if (!ShowVideoList) Progress = item.Progress;
                        }
                    }
                });
            });

            try
            {
                await _ytDlp.DownloadAsync(item.Url, settings.DownloadPath, SelectedFormat, progress, token, SelectedFormatOption?.FormatId);

                item.IsDownloaded = true;
                item.Progress = 1.0;
                item.Status = Strings.Done;

                await _history.AddAsync(new DownloadRecord
                {
                    Title = item.Title,
                    Url = item.Url,
                    Channel = item.Channel,
                    ThumbnailUrl = item.ThumbnailUrl,
                    Format = SelectedFormat,
                    DownloadPath = settings.DownloadPath,
                    DownloadedAt = DateTime.Now,
                    Success = true
                });
            }
            catch (OperationCanceledException)
            {
                item.Status = Strings.Cancelled;
            }
            catch (Exception ex)
            {
                item.Status = $"❌ {ex.Message}";
                await _history.AddAsync(new DownloadRecord
                {
                    Title = item.Title,
                    Url = item.Url,
                    Format = SelectedFormat,
                    DownloadedAt = DateTime.Now,
                    Success = false
                });
            }
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    #endregion
}