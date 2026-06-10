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

    public LibraryPage()
    {
        InitializeComponent();
    }

    public LibraryPage(LibraryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var settings = new SettingsService().Load();
        _vm.LoadFromSettings(settings);
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PropertyChanged -= OnVmPropertyChanged;
        Player.Pause();
    }

    // ── Cambios del ViewModel ─────────────────────────────────────────

    private async void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.CurrentItem) && _vm.CurrentItem is not null)
        {
            _isLoadingNewItem = true;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Player.Stop();
                Player.Source = MediaSource.FromFile(_vm.CurrentItem.FilePath);
                // Play lo dispara Player_MediaOpened
            });
        }
        else if (e.PropertyName == nameof(LibraryViewModel.IsPlaying) && !_isLoadingNewItem)
        {
            if (_vm.IsPlaying) Player.Play();
            else Player.Pause();
        }
    }

    // ── Eventos MediaElement ──────────────────────────────────────────

    private async void Player_MediaOpened(object? sender, EventArgs e)
    {
        await Task.Delay(150);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _isLoadingNewItem = false; // ya está listo
            Player.Volume = _vm.Volume;
            Player.Play();
        });
    }

    private void Player_PositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        if (!_isSeeking)
            _vm.UpdateProgress(e.Position.TotalSeconds, Player.Duration.TotalSeconds);
    }

    private async void Player_MediaEnded(object? sender, EventArgs e)
    {
        if (_vm.IsRepeatOn)
        {
            await Player.SeekTo(TimeSpan.Zero, CancellationToken.None);
            Player.Play();
        }
        else
        {
            // Diferir para salir del stack del evento MediaEnded antes de tocar el Player
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() => _vm.OnMediaEnded());
        }
    }

    // ── Eventos UI ────────────────────────────────────────────────────

    private async void Slider_DragCompleted(object? sender, EventArgs e)
    {
        _isSeeking = false;
        if (sender is Slider s)
            await Player.SeekTo(TimeSpan.FromSeconds(s.Value), CancellationToken.None);
    }

    private void VolumeSlider_ValueChanged(object? sender, ValueChangedEventArgs e)
    {
        Player.Volume = e.NewValue;
        _vm.Volume = e.NewValue;
    }
}