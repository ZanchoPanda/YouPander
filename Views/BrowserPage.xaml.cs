

namespace YouPander.Views;

[QueryProperty(nameof(Url), "url")]
public partial class BrowserPage : ContentPage
{
    private string _url = string.Empty;
    public string Url
    {
        get => _url;
        set
        {
            _url = Uri.UnescapeDataString(value ?? string.Empty);
            // Cargar la URL cuando el control esté listo
            if (WebViewControl != null)
                WebViewControl.Source = new UrlWebViewSource { Url = _url };
        }
    }

    public BrowserPage()
    {
        InitializeComponent();
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(_url))
            WebViewControl.Source = new UrlWebViewSource { Url = _url };
    }

    private void OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        // Actualizar la URL visible mientras navega
        LblUrl.Text = e.Url;
    }

    private void OnNavigated(object sender, WebNavigatedEventArgs e)
    {
        LblUrl.Text = e.Url;

        // Actualizar estado de los botones tras cada navegación
        BtnBack.IsEnabled = WebViewControl.CanGoBack;
        BtnForward.IsEnabled = WebViewControl.CanGoForward;
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        if (WebViewControl.CanGoBack)
            WebViewControl.GoBack();
    }

    private void OnForwardClicked(object sender, EventArgs e)
    {
        if (WebViewControl.CanGoForward)
            WebViewControl.GoForward();
    }

    private void OnReloadClicked(object sender, EventArgs e)
    {
        WebViewControl.Reload();
    }

    protected override bool OnBackButtonPressed()
    {
        if (WebViewControl.CanGoBack)
        {
            WebViewControl.GoBack();
            return true;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.GoToAsync("//MainPage");
        });
        return true;
    }

}