using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VChibi.Models;
using VChibi.Runtime;
namespace VChibi.Controls;

public partial class SpritePreview : UserControl
{
    private SpritePlayer _player = new();
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private double _accumulatedSeconds;
    private string _actionName = "idle";
    private bool _isPlaying;

    public SpritePreview()
    {
        InitializeComponent();

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 Hz
        };
        _timer.Tick += OnTick;

        IsVisibleChanged += OnIsVisibleChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void LoadPet(PetDefinition? pet, string actionName = "idle")
    {
        _actionName = actionName;
        _accumulatedSeconds = 0;

        if (pet == null)
        {
            Stop();
            _player = new SpritePlayer();
            PreviewImage.Source = null;
            PlaceholderText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            _player.Load(pet, pet.RootDirectory);
            PlaceholderText.Visibility = Visibility.Collapsed;

            // Render first frame immediately
            var frame = _player.GetFrame(_actionName, 0, out _);
            PreviewImage.Source = frame;

            if (IsVisible)
            {
                Play();
            }
        }
        catch
        {
            Stop();
            PreviewImage.Source = null;
            PlaceholderText.Visibility = Visibility.Visible;
        }
    }

    public void SetAction(string actionName)
    {
        _actionName = actionName;
        _accumulatedSeconds = 0;
        if (_player.IsLoaded)
        {
            var frame = _player.GetFrame(_actionName, 0, out _);
            PreviewImage.Source = frame;
        }
    }

    public void SetHorizontalFlip(bool flip)
    {
        PreviewScaleTransform.ScaleX = flip ? -1.0 : 1.0;
    }

    public void LoadDraftFrames(int fps, IReadOnlyList<BitmapSource> frames)
    {
        _actionName = "idle";
        _accumulatedSeconds = 0;

        if (frames == null || frames.Count == 0)
        {
            Stop();
            _player = new SpritePlayer();
            PreviewImage.Source = null;
            PlaceholderText.Visibility = Visibility.Visible;
            return;
        }

        _player = new SpritePlayer();
        _player.SetSingleAction(fps, frames);
        PlaceholderText.Visibility = Visibility.Collapsed;

        var frame = _player.GetFrame("idle", 0, out _);
        PreviewImage.Source = frame;

        if (IsVisible)
        {
            Play();
        }
    }

    public void UpdateFps(int fps)
    {
        if (_player.IsLoaded)
        {
            _player.UpdateFps(_actionName, fps);
        }
    }

    public bool IsPlaying => _isPlaying;

    public void Play()
    {
        if (_player.IsLoaded && !_isPlaying)
        {
            _isPlaying = true;
            _stopwatch.Restart();
            _timer.Start();
        }
    }

    public void Stop()
    {
        _isPlaying = false;
        _timer.Stop();
        _stopwatch.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_isPlaying || !_player.IsLoaded || !IsVisible)
        {
            return;
        }

        double dt = _stopwatch.Elapsed.TotalSeconds;
        _stopwatch.Restart();

        if (dt > 0.2) dt = 0.2;
        _accumulatedSeconds += dt;

        var frame = _player.GetFrame(_actionName, _accumulatedSeconds, out _);
        if (frame != null)
        {
            PreviewImage.Source = frame;
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            if (_player.IsLoaded) Play();
        }
        else
        {
            Stop();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsVisible && _player.IsLoaded)
        {
            Play();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Stop();
    }
}
