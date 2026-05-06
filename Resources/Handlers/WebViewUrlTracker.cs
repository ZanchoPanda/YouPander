using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace YouPander.Resources.Handlers
{
    public class WebViewUrlTracker : IDisposable
    {

        private readonly WebView _webView;
        private readonly Action<string> _onUrlChanged;
        private CancellationTokenSource? _cts;

        public WebViewUrlTracker(WebView webView, Action<string> onUrlChanged)
        {
            _webView = webView;
            _onUrlChanged = onUrlChanged;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () =>
            {
                string lastUrl = string.Empty;

                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token).ContinueWith(_ => { });
                    if (token.IsCancellationRequested) break;

                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            var url = await _webView.EvaluateJavaScriptAsync("window.location.href");
                            if (!string.IsNullOrWhiteSpace(url) && url != "null" && url != lastUrl)
                            {
                                lastUrl = url;
                                _onUrlChanged(url);
                            }
                        }
                        catch { }
                    });
                }
            }, token);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

    }
}
