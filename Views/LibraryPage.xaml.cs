
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using YouPander.Services;
using YouPander.ViewModels;

namespace YouPander.Views;

public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _vm;

    private bool _isSeeking;
    private bool _isLoadingNewItem;
    private bool _isMediaReady;
    private bool _isPageActive;

    private CancellationTokenSource? _mediaLoadCts;
    private int _loadVersion;

    public LibraryPage(LibraryViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        BindingContext = _vm;
    }

    // ── Ciclo de vida ─────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _isPageActive = true;

        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.PropertyChanged += OnVmPropertyChanged;

        var settings = new SettingsService().Load();
        _vm.LoadFromSettings(settings);
    }

    protected override void OnDisappearing()
    {
        _isPageActive = false;

        _vm.PropertyChanged -= OnVmPropertyChanged;

        _mediaLoadCts?.Cancel();

        _isMediaReady = false;
        _isLoadingNewItem = false;
        _isSeeking = false;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Player.Pause();
        });

        base.OnDisappearing();
    }

    // ── Cambios del ViewModel ─────────────────────────────────────

    private async void OnVmPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.CurrentItem))
        {
            var item = _vm.CurrentItem;

            if (item is not null)
                await LoadMediaAsync(item.FilePath);

            return;
        }

        if (e.PropertyName == nameof(LibraryViewModel.IsPlaying))
        {
            if (_isLoadingNewItem || !_isMediaReady || !_isPageActive)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_vm.IsPlaying)
                    Player.Play();
                else
                    Player.Pause();
            });

            return;
        }

        if (e.PropertyName == nameof(LibraryViewModel.Volume))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Player.Volume = _vm.Volume;
            });
        }
    }

    // ── Carga de medios ────────────────────────────────────────────

    private async Task LoadMediaAsync(string filePath)
    {
        if (!_isPageActive)
            return;

        // Invalidar cualquier carga anterior.
        _mediaLoadCts?.Cancel();
        _mediaLoadCts?.Dispose();

        _mediaLoadCts = new CancellationTokenSource();

        var token = _mediaLoadCts.Token;
        int version = ++_loadVersion;

        _isLoadingNewItem = true;
        _isMediaReady = false;
        _isSeeking = false;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!_isPageActive || token.IsCancellationRequested)
                    return;

                // Detener la reproducción anterior.
                Player.Stop();

                // Cambiar el origen multimedia.
                Player.Source = MediaSource.FromFile(filePath);
            });
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                _isLoadingNewItem = false;
                _isMediaReady = false;
            }

            System.Diagnostics.Debug.WriteLine(
                $"Error cargando medio: {ex}");
        }
    }

    // ── Eventos MediaElement ──────────────────────────────────────

    private async void Player_MediaOpened(
        object? sender,
        EventArgs e)
    {
        if (!_isPageActive)
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!_isPageActive)
                    return;

                _isMediaReady = true;
                _isLoadingNewItem = false;

                Player.Volume = _vm.Volume;

                _vm.UpdateProgress(
                    0,
                    Player.Duration.TotalSeconds);

                _vm.IsPlaying = true;

                Player.Play();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Error inicializando reproductor: {ex}");
        }
    }

    private void Player_PositionChanged(
        object? sender,
        MediaPositionChangedEventArgs e)
    {
        if (!_isPageActive ||
            !_isMediaReady ||
            _isSeeking ||
            _isLoadingNewItem)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_isPageActive ||
                !_isMediaReady ||
                _isSeeking ||
                _isLoadingNewItem)
                return;

            _vm.UpdateProgress(
                e.Position.TotalSeconds,
                Player.Duration.TotalSeconds);
        });
    }

    private async void Player_MediaEnded(
        object? sender,
        EventArgs e)
    {
        if (!_isPageActive || !_isMediaReady)
            return;

        try
        {
            if (_vm.IsRepeatOn)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!_isPageActive || !_isMediaReady)
                        return;

                    await Player.SeekTo(
                        TimeSpan.Zero,
                        CancellationToken.None);

                    Player.Play();
                });
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _isMediaReady = false;
                    _vm.OnMediaEnded();
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Error al finalizar reproducción: {ex}");
        }
    }

    // ── Eventos Slider ────────────────────────────────────────────

    private void Slider_DragStarted(
        object? sender,
        EventArgs e)
    {
        if (_isMediaReady && !_isLoadingNewItem)
            _isSeeking = true;
    }

    private async void Slider_DragCompleted(
        object? sender,
        EventArgs e)
    {
        if (sender is not Slider slider ||
            !_isPageActive ||
            !_isMediaReady ||
            _isLoadingNewItem)
        {
            _isSeeking = false;
            return;
        }

        try
        {
            var target = TimeSpan.FromSeconds(slider.Value);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (!_isPageActive || !_isMediaReady)
                    return;

                await Player.SeekTo(
                    target,
                    CancellationToken.None);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Error realizando Seek: {ex}");
        }
        finally
        {
            _isSeeking = false;
        }
    }

    // ── Volumen ───────────────────────────────────────────────────

    private void VolumeSlider_ValueChanged(
        object? sender,
        ValueChangedEventArgs e)
    {
        if (!_isPageActive)
            return;

        Player.Volume = e.NewValue;
        _vm.Volume = e.NewValue;
    }
}
