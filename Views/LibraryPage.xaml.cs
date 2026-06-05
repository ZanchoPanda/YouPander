using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using YouPander.Services;
using YouPander.ViewModels;

namespace YouPander.Views;

public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _vm;
    private bool _isSeeking;

    public LibraryPage(LibraryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Cargar la config guardada
        var settings = new SettingsService().Load();
        _vm.LoadFromSettings(settings);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Player.Pause();
    }

    // Sincroniza la fuente del MediaElement cuando cambia la pista
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.CurrentItem) && _vm.CurrentItem is not null)
            {
                Player.Stop();
                Player.Source = MediaSource.FromFile(_vm.CurrentItem.FilePath);
            }
            else if (e.PropertyName == nameof(LibraryViewModel.IsPlaying))
            {
                if (_vm.IsPlaying) Player.Play();
                else Player.Pause();
            }
        };
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
            _vm.OnMediaEnded();
        }
        //_vm.OnMediaEnded();
        //if (_vm.IsRepeatOn)
        //    Player.SeekTo(TimeSpan.Zero);
    }

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

    private async void Player_MediaOpened(object sender, EventArgs e)
    {
        try
        {
            Player.Volume = _vm.Volume;
            await Task.Delay(100);
            if (_vm.IsPlaying)
                Player.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"INNER: {ex.InnerException?.Message ?? ex.Message}");
            System.Diagnostics.Debug.WriteLine($"STACK: {ex.InnerException?.StackTrace ?? ex.StackTrace}");
        }
    }
}