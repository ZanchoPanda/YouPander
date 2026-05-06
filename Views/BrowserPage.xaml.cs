

using System.Collections.ObjectModel;
using YouPander.Models;
using YouPander.Resources.Handlers;
using YouPander.Services;

namespace YouPander.Views;

[QueryProperty(nameof(Url), "url")]
public partial class BrowserPage : ContentPage
{
    private readonly YtDlpService? _ytDlp;
    private readonly SettingsService _settings;
    private readonly HistoryService _history;
    private WebViewUrlTracker? _tracker;

    #region Properties

    private string _url = string.Empty;
    public string Url
    {
        get => _url;
        set
        {
            _url = Uri.UnescapeDataString(value ?? string.Empty);
            OnPropertyChanged();
            // Cargar la URL cuando el control esté listo
            if (WebViewControl != null && WebViewControl.Source is UrlWebViewSource current
            && current.Url != _url)
            {
                WebViewControl.Source = new UrlWebViewSource { Url = _url };
            }
        }
    }

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

    public BrowserPage()
    {
        InitializeComponent();

        _settings = new SettingsService();
        _history = new HistoryService();

        if (OperatingSystem.IsWindows())
        {
            var ytPath = Path.Combine(FileSystem.AppDataDirectory, "yt-dlp.exe");
            _ytDlp = new YtDlpService(ytPath);
        }
    }

    protected override void OnAppearing()
    {
        #region Version 1

        //base.OnAppearing();
        //if (!string.IsNullOrEmpty(_url))
        //    WebViewControl.Source = new UrlWebViewSource { Url = _url };
        #endregion

        #region Version 2
        base.OnAppearing();
        if (!string.IsNullOrEmpty(_url))
            WebViewControl.Source = new UrlWebViewSource { Url = _url };

        _tracker = new WebViewUrlTracker(WebViewControl, url =>
        {
            _url = url;
            LblUrl.Text = url;
        });
        _tracker.Start();
        #endregion
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _tracker?.Dispose();
    }

    private void OnReloadClicked(object sender, EventArgs e)
    {
        WebViewControl.Reload();
    }

    protected override bool OnBackButtonPressed()
    {
        //Desactivado para evitar hacer tantos clicks en flecha

        //if (WebViewControl.CanGoBack)
        //{
        //    WebViewControl.GoBack();
        //    return true;
        //}

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.GoToAsync("//MainPage");
        });
        return true;
    }

    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        try
        {


            if (string.IsNullOrWhiteSpace(Url) || IsDownloading)
            {
                return;
            }

            IsDownloading = true;
            OnPropertyChanged(nameof(IsDownloading));

            string UrlLimpia = _ytDlp.CleanYouTubeUrl(Url);

            var settings = _settings.Load();
            string FinalDownloadPath = settings.DownloadPath;

            var infos = await _ytDlp.FetchInfoAsync(UrlLimpia);
            VideoInfo Video = infos.FirstOrDefault();

            var formats = await _ytDlp.FetchFormatsAsync(UrlLimpia);
            AvailableFormats = new ObservableCollection<FormatOption>(formats);
            SelectedFormatOption = AvailableFormats.FirstOrDefault();

            if (settings.AdvancedConfig)
            {
                if ((SelectedFormatOption?.IsVideo ?? false) && !string.IsNullOrWhiteSpace(settings.VideoDownloadPath))
                {
                    Directory.CreateDirectory(settings.VideoDownloadPath);
                    FinalDownloadPath = settings.VideoDownloadPath;
                }
                else if (!string.IsNullOrWhiteSpace(settings.AudioDownloadPath))
                {
                    Directory.CreateDirectory(settings.AudioDownloadPath);
                    FinalDownloadPath = settings.AudioDownloadPath;
                }
            }

            await _ytDlp.DownloadAsync(UrlLimpia, FinalDownloadPath, SelectedFormatOption.Label, SelectedFormatOption?.FormatId);

            if (Video != null)
            {
                await _history.AddAsync(new DownloadRecord
                {
                    Title = Video.Title,
                    Url = Video.Url,
                    Channel = Video.Channel,
                    ThumbnailUrl = Video.Thumbnail,
                    Format = SelectedFormatOption?.Label ?? string.Empty,
                    DownloadPath = FinalDownloadPath,
                    DownloadedAt = DateTime.Now,
                    Success = true
                });
            }

            if (settings.OpenDownloads)
                await OpenDownloads();

        }
        catch (Exception ex)
        {
            var aux = ex;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    public async Task OpenDownloads()
    {
        var settings = _settings.Load();
        Page? page = Application.Current?.Windows?.FirstOrDefault()?.Page;

        try
        {
            string FinalDownloadPath = settings.DownloadPath;
            if (settings.AdvancedConfig)
            {
                if ((SelectedFormatOption?.IsVideo ?? false) && !string.IsNullOrWhiteSpace(settings.VideoDownloadPath))
                {
                    Directory.CreateDirectory(settings.VideoDownloadPath);
                    FinalDownloadPath = settings.VideoDownloadPath;
                }
                else if (!string.IsNullOrWhiteSpace(settings.AudioDownloadPath))
                {
                    Directory.CreateDirectory(settings.AudioDownloadPath);
                    FinalDownloadPath = settings.AudioDownloadPath;
                }
            }

            await Launcher.OpenAsync(FinalDownloadPath);
        }
        catch (Exception ex)
        {

        }
    }

}