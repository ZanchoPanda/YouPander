

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

            #region V1

            //// Cargar la URL cuando el control esté listo
            //if (WebViewControl != null && WebViewControl.Source is UrlWebViewSource current
            //&& current.Url != _url)
            //{
            //    WebViewControl.Source = new UrlWebViewSource { Url = _url };
            //}
            #endregion

            #region V2
            MainThread.BeginInvokeOnMainThread(() => CargarUrl(_url));
            #endregion
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
        //base.OnAppearing();
        //if (!string.IsNullOrEmpty(_url))
        //    WebViewControl.Source = new UrlWebViewSource { Url = _url };

        //_tracker = new WebViewUrlTracker(WebViewControl, url =>
        //{
        //    _url = url;
        //    LblUrl.Text = url;
        //});
        //_tracker.Start();
        #endregion

        #region V3
        base.OnAppearing();

        if (WebViewControl.Handler != null)
        {
            InicializarWebView();
        }
        else
        {
            WebViewControl.HandlerChanged += OnHandlerReady;
        }
        #endregion
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WebViewControl.HandlerChanged -= OnHandlerReady;
        _tracker?.Dispose();
    }

    private void OnHandlerReady(object? sender, EventArgs e)
    {
        WebViewControl.HandlerChanged -= OnHandlerReady;
        InicializarWebView();
    }

    private async void InicializarWebView()
    {
        #region Version log
        string logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YouPander", "webview_log.txt"
    );

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, $"[{DateTime.Now}] Iniciando\n");

#if WINDOWS
        var platformView = WebViewControl.Handler?.PlatformView
            as Microsoft.Maui.Platform.MauiWebView;
        File.AppendAllText(logPath, $"MauiWebView null: {platformView == null}\n");

        if (platformView != null)
        {
            var tcs = new TaskCompletionSource<bool>();

    platformView.CoreWebView2Initialized += (s, e) =>
    {
        File.AppendAllText(logPath, $"CoreWebView2Initialized disparado. Error: {e.Exception?.Message ?? "ninguno"}\n");
        if (e.Exception != null)
            tcs.TrySetException(e.Exception);
        else
            tcs.TrySetResult(true);
    };

    File.AppendAllText(logPath, "Estableciendo Source para forzar inicialización...\n");
    platformView.Source = new Uri("about:blank");

    File.AppendAllText(logPath, "Esperando CoreWebView2Initialized...\n");
    var completado = await Task.WhenAny(tcs.Task, Task.Delay(5000));

    if (completado == tcs.Task && tcs.Task.Result)
        File.AppendAllText(logPath, "CoreWebView2 inicializado OK\n");
    else
        File.AppendAllText(logPath, "Timeout esperando CoreWebView2\n");
        }
#endif
            File.AppendAllText(logPath, "Esperando 500ms antes de cargar URL...\n");
            await Task.Delay(500);

            File.AppendAllText(logPath, "Llamando CargarUrl...\n");
            CargarUrl(_url);
            File.AppendAllText(logPath, "CargarUrl OK\n");

            _tracker?.Dispose();
            _tracker = new WebViewUrlTracker(WebViewControl, url =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _url = url;
                    LblUrl.Text = url;
                });
            });
            _tracker.Start();
            File.AppendAllText(logPath, "Tracker OK\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"ERROR: {ex.GetType().Name}\n");
            File.AppendAllText(logPath, $"Mensaje: {ex.Message}\n");
            File.AppendAllText(logPath, $"StackTrace: {ex.StackTrace}\n");
            await DisplayAlert("Error WebView", ex.Message, "OK");
        }
        #endregion

        #region Version 

        //try
        //{
        //    // Forzar inicialización del CoreWebView2 antes de cargar URL
        //    await WebViewControl.EvaluateJavaScriptAsync("1");
        //}
        //catch { }

        //try
        //{
        //    CargarUrl(_url);

        //    _tracker?.Dispose();
        //    _tracker = new WebViewUrlTracker(WebViewControl, url =>
        //    {
        //        MainThread.BeginInvokeOnMainThread(() =>
        //        {
        //            _url = url;
        //            LblUrl.Text = url;
        //        });
        //    });
        //    _tracker.Start();
        //}
        //catch (Exception ex)
        //{
        //    MainThread.BeginInvokeOnMainThread(async () =>
        //        await DisplayAlert("Error WebView", ex.Message, "OK"));
        //}
        #endregion
    }

    private void CargarUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (WebViewControl == null) return;

        // Eliminada la comprobación de URL igual para forzar siempre la carga
        WebViewControl.Source = new UrlWebViewSource { Url = url };

        if (LblUrl != null)
        {
            LblUrl.Text = url;
        }
    }

    private async void OnReloadClicked(object sender, EventArgs e)
    {
        if (WebViewControl?.Handler?.PlatformView == null) return;

        try
        {
            await WebViewControl.EvaluateJavaScriptAsync("1");
            WebViewControl.Reload();
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", $"No se pudo recargar: {ex.Message}", "OK");
        }
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