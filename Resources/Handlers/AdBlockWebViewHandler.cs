using Microsoft.Maui.Handlers;
using Microsoft.Maui.Controls;

#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
#endif

namespace YouPander.Handlers;

public class AdBlockWebViewHandler : WebViewHandler
{
    // Dominios y patrones a bloquear
    private static readonly string[] BlockedPatterns =
    [
        "googlesyndication.com",
        "doubleclick.net",
        "googleadservices.com",
        "google-analytics.com",
        "googletagmanager.com",
        "googletagservices.com",
        "youtube.com/pagead",
        "youtube.com/get_video_info",
        "youtube.com/api/stats/ads",
        "youtube.com/api/stats/atr",
        "youtube.com/api/stats/qoe",
        "static.doubleclick.net",
        "ad.doubleclick.net",
        "ads.youtube.com",
        "www.youtube.com/ptracking",
        "yt3.ggpht.com/ytts",
        "youtubei.googleapis.com/youtubei/v1/log_event",
    ];

#if WINDOWS
    protected override void ConnectHandler(WebView2 platformView)
    {
        base.ConnectHandler(platformView);
        platformView.CoreWebView2Initialized += OnCoreWebView2Initialized;
    }

    private void OnCoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs e)
    {
        sender.CoreWebView2.WebResourceRequested += OnWebResourceRequested;

        // Suscribirse a todos los tipos de recursos
        sender.CoreWebView2.AddWebResourceRequestedFilter(
            "*",
            CoreWebView2WebResourceContext.All
        );

        // Inyectar CSS para ocultar elementos de anuncios que no se bloquean por red
        sender.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
    }

    private void OnWebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var url = e.Request.Uri;

        if (BlockedPatterns.Any(pattern => url.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            // Bloquear la petición devolviendo una respuesta vacía
            e.Response = sender.Environment.CreateWebResourceResponse(
                null, 200, "OK", "Content-Type: text/plain"
            );
        }
    }

    private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        const string adBlockCss = 
        ".ad-showing .html5-main-video { opacity: 1 !important; }" +
        ".ytp-ad-overlay-container," +
        ".ytp-ad-text-overlay," +
        ".ytp-ad-skip-button-container," +
        ".ytp-ad-module," +
        ".ytp-ad-player-overlay," +
        ".ytp-ad-progress," +
        ".ytp-ad-progress-list," +
        "#masthead-ad," +
        "#player-ads," +
        "ytd-banner-promo-renderer," +
        "ytd-statement-banner-renderer," +
        "ytd-ad-slot-renderer," +
        "ytd-in-feed-ad-layout-renderer," +
        "ytd-promoted-sparkles-web-renderer," +
        "ytd-promoted-video-renderer," +
        "tp-yt-paper-dialog:has(ytd-ad-slot-renderer)" +
        "{ display: none !important; }";

    string adBlockJs =
        "(function() {" +
        "  const style = document.createElement('style');" +
        "  style.textContent = `" + adBlockCss + "`;" +
        "  document.head?.appendChild(style);" +
        "  const skipAd = () => {" +
        "    const skipBtn = document.querySelector('.ytp-skip-ad-button, .ytp-ad-skip-button');" +
        "    if (skipBtn) skipBtn.click();" +
        "    const adVideo = document.querySelector('.ad-showing video');" +
        "    if (adVideo) { adVideo.currentTime = adVideo.duration; }" +
        "  };" +
        "  const observer = new MutationObserver(skipAd);" +
        "  observer.observe(document.body, { childList: true, subtree: true });" +
        "  setInterval(skipAd, 1000);" +
        "})();";

    await sender.ExecuteScriptAsync(adBlockJs);
    }

    protected override void DisconnectHandler(WebView2 platformView)
    {
        platformView.CoreWebView2Initialized -= OnCoreWebView2Initialized;
        base.DisconnectHandler(platformView);
    }
#endif
}