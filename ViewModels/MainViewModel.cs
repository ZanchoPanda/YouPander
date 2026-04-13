using YouPander.Resources.Localization;
using YouPander.Services;
using YouPander.ViewModels;

public class MainViewModel : BaseViewModel
{
    #region Fields

    private CancellationTokenSource? _cts;
    private readonly YtDlpService? _ytDlp;
    private readonly SettingsService _settings;

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
                OnPropertyChanged(nameof(HasError)); // para binding en la View
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    #endregion

    #region Commands

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

        DownloadCommand = new Command(async () => await Download(), () => !IsDownloading);
        CancelCommand = new Command(async () => await CancelDownload(), () => IsDownloading);
        OpenSettingsCommand = new Command(async () => await Shell.Current.GoToAsync("///SettingsPage"));
    }

    private async Task Download()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Url))
            return;

        var settings = _settings.Load();

        if (string.IsNullOrWhiteSpace(settings.DownloadPath))
            return;

        if (!OperatingSystem.IsWindows() || _ytDlp == null)
        {
            Status = Strings.OnlyAvailableOnWindows; // Localizado
            return;
        }

        _cts = new CancellationTokenSource();
        IsDownloading = true;
        NotifyCommandsCanExecuteChanged();

        try
        {
            var progress = new Progress<string>(line =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (line.StartsWith("⚠️"))
                    {
                        ErrorMessage = line;
                        return;
                    }

                    Status = line;

                    if (line.Contains('%'))
                    {
                        var parts = line.Split('%')[0].Split(' ');
                        if (double.TryParse(parts.Last(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double value))
                        {
                            Progress = value / 100.0;
                        }
                    }
                });
            });

            await _ytDlp.EnsureInstalledAsync();
            await _ytDlp.EnsureFfmpegInstalledAsync();
            await _ytDlp.DownloadAsync(Url, settings.DownloadPath, SelectedFormat, progress, _cts.Token);

            if (settings.OpenDownloads)
                await OpenDownloads();
        }
        catch (OperationCanceledException)
        {
            // Cancelado por el usuario, no es un error
        }
        catch (Exception ex)
        {
            Page? page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
                await page.DisplayAlertAsync(Strings.Error, ex.Message, Strings.Ok);
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Progress = 0;
                Status = string.Empty;
                IsDownloading = false;
                NotifyCommandsCanExecuteChanged();
            });
        }
    }

    private async Task CancelDownload()
    {
        _cts?.Cancel();
        _ytDlp?.KillCurrentProcess();

        Progress = 0;
        Status = $"{Strings.CancelDownloads}...";

        await Task.Delay(300); // Pequeña pausa para que el proceso muera limpiamente
        Status = string.Empty;
        IsDownloading = false;
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
        DownloadCommand.ChangeCanExecute();
        CancelCommand.ChangeCanExecute();
    }
}