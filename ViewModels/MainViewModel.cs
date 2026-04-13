using YouPander.Resources.Localization;
using YouPander.Services;
using YouPander.ViewModels;

public class MainViewModel : BaseViewModel
{
    #region Properties

    private CancellationTokenSource? _cts;
    private readonly YtDlpService? _ytDlp;
    private readonly SettingsService _settings;

    public string Url { get; set; } = string.Empty;
    //private string _Url;
    //public string Url
    //{
    //    get
    //    {
    //        return _Url;
    //    }
    //    set
    //    {
    //        if (value != _Url)
    //        {
    //            _Url = value;
    //            OnPropertyChanged("Url");
    //        }
    //    }
    //}

    //public string SelectedFormat { get; set; } = "Video";
    private string _SelectedFormat;
    public string SelectedFormat
    {
        get
        {
            return _SelectedFormat;
        }
        set
        {
            if (value != _SelectedFormat)
            {
                _SelectedFormat = value;
                OnPropertyChanged("SelectedFormat");
            }
        }
    }


    private double _Progress;
    public double Progress
    {
        get
        {
            return _Progress;
        }
        set
        {
            if (value != _Progress)
            {
                _Progress = value;
                OnPropertyChanged("Progress");
            }
        }
    }

    private string _Status;
    public string Status
    {
        get
        {
            return _Status;
        }
        set
        {
            if (value != _Status)
            {
                _Status = value;
                OnPropertyChanged("Status");
            }
        }
    }

    private bool _IsDownloading;
    public bool IsDownloading
    {
        get
        {
            return _IsDownloading;
        }
        set
        {
            if (value != _IsDownloading)
            {
                _IsDownloading = value;
                OnPropertyChanged("IsDownloading");
            }
        }
    }

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
        else
        {
            _ytDlp = null;
        }
        SelectedFormat = "Video";

        DownloadCommand = new Command(async () => await Download());
        CancelCommand = new Command(async () => await CancelDownload());

        OpenSettingsCommand = new Command(async () =>
            await Shell.Current.GoToAsync("///SettingsPage"));
    }

    private async Task Download()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            return;
        }

        var settings = _settings.Load();

        if (string.IsNullOrWhiteSpace(settings.DownloadPath))
        {
            return;
        }

        if (!OperatingSystem.IsWindows() || _ytDlp == null)
        {
            Status = $"La descarga solo está disponible en Windows.";
            Progress = 0;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Progress));
            return;
        }

        _cts = new CancellationTokenSource();
        IsDownloading = true;

        var progress = new Progress<string>(line =>
        {
            Status = line;

            if (line.Contains("%"))
            {
                var percentText = line.Split('%')[0]
                    .Split(' ')
                    .Last();

                if (double.TryParse(percentText, out double value))
                    Progress = value / 100;
            }
        });

        await _ytDlp.EnsureInstalledAsync();

        await _ytDlp.DownloadAsync(
            Url,
            settings.DownloadPath,
            SelectedFormat,
            progress, _cts.Token
        );

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Progress = 0;           // barra vacía
            Status = string.Empty;  // texto limpio
        });

        if (settings.OpenDownloads)
        {
            await OpenDownloads();
        }

        IsDownloading = false;

    }

    private async Task CancelDownload()
    {
        IsDownloading = false;

        if (_ytDlp?.currentProcess != null)
        {
            _ytDlp?.currentProcess.Kill();
            _ytDlp?.currentProcess = null;
        }

        Progress = 0;
        Status = $"{Strings.CancelDownloads}...";

        await OpenDownloads();
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
            {
                await page.DisplayAlertAsync(
                    Strings.Error,
                    $"{Strings.CannotOpenFolder}: {ex.Message}",
                    Strings.Ok
                );
            }
        }
    }

}