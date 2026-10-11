using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using VChibi.Models;
using VChibi.Platform.Windows;
using VChibi.Views;

namespace VChibi.Runtime;

public class DesktopPetSession
{
    private readonly PetWindow _window;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();

    private SpritePlayer _player = new();
    private PetController _controller = new();
    private int _generation = 0;

    private double _scalePercent = 100.0;
    private double _movementSpeedDip = 40.0;
    private double _accumulatedAnimationSeconds = 0.0;

    public bool IsVisible { get; private set; }
    public PetWindow Window => _window;
    public PetController Controller => _controller;
    public SpritePlayer Player => _player;

    public double ScalePercent => _scalePercent;
    public double MovementSpeedDip => _movementSpeedDip;

    public bool IsTimerRunning => _timer.IsEnabled;

    public DesktopPetSession(PetWindow? window = null)
    {
        _window = window ?? new PetWindow();

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 Hz
        };
        _timer.Tick += OnTick;

        try
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }
        catch { }
    }

    public bool TryActivatePet(PetDefinition definition)
    {
        var targetGen = Interlocked.Increment(ref _generation);

        // Preload frames before committing session; if load fails, keep old pet intact
        var newPlayer = SpritePlayer.LoadFromDefinition(definition, definition.RootDirectory);
        if (!newPlayer.IsLoaded)
        {
            return false;
        }

        if (targetGen != _generation)
        {
            // Stale generation superseded by newer load request
            return false;
        }

        _player = newPlayer;
        _accumulatedAnimationSeconds = 0.0;

        UpdateLayoutAndBounds();

        // Initial frame
        var frame = _player.GetFrame("idle", 0.0, out _);
        _window.SetFrame(frame, false);
        return true;
    }

    public void ActivatePet(PetDefinition definition)
    {
        if (!TryActivatePet(definition))
        {
            throw new InvalidOperationException($"Không thể nạp frame animation cho pet '{definition.Name}' hoặc thao tác nạp bị hủy bởi yêu cầu mới hơn.");
        }
    }

    public void SetDisplaySettings(double scalePercent, double movementSpeedDip)
    {
        _scalePercent = Math.Clamp(scalePercent, PetDisplaySettings.MinScale, PetDisplaySettings.MaxScale);
        _movementSpeedDip = Math.Clamp(movementSpeedDip, PetDisplaySettings.MinSpeed, PetDisplaySettings.MaxSpeed);

        UpdateLayoutAndBounds();
    }

    private void UpdateLayoutAndBounds()
    {
        var workArea = ScreenWorkspace.GetPrimaryWorkArea(_window.Handle);

        double widthDip = _player.MaxPixelWidth * (_scalePercent / 100.0);
        double heightDip = _player.MaxPixelHeight * (_scalePercent / 100.0);

        _window.SetDimensions(widthDip, heightDip);

        int widthPx = workArea.DipToPx(widthDip);
        int heightPx = workArea.DipToPx(heightDip);

        _controller.Movement.UpdateBounds(workArea, widthPx, heightPx);
    }

    public void Show()
    {
        if (IsVisible) return;

        _window.Show();
        IsVisible = true;

        // Recompute with the live window handle's actual monitor DPI
        UpdateLayoutAndBounds();

        _stopwatch.Restart();
        _timer.Start();
    }

    public void Hide()
    {
        if (!IsVisible) return;

        _timer.Stop();
        _stopwatch.Stop();

        _window.Hide();
        IsVisible = false;
    }

    public void Stop()
    {
        Hide();
        Interlocked.Increment(ref _generation); // Invalidate any in-flight load
        _timer.Tick -= OnTick;
        try
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
        catch { }
        _player = new SpritePlayer();
        _window.ClearFrame();
        try
        {
            _window.Close();
        }
        catch { }
    }

    private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        try
        {
            _window.Dispatcher.InvokeAsync(() =>
            {
                if (e.Mode == Microsoft.Win32.PowerModes.Suspend)
                {
                    _timer.Stop();
                }
                else if (e.Mode == Microsoft.Win32.PowerModes.Resume)
                {
                    _stopwatch.Restart();
                    if (IsVisible)
                    {
                        _timer.Start();
                    }
                }
            });
        }
        catch { }
    }

    internal void HandlePowerModeChangedForTest(Microsoft.Win32.PowerModes mode)
    {
        if (_window.Dispatcher.CheckAccess())
        {
            if (mode == Microsoft.Win32.PowerModes.Suspend)
            {
                _timer.Stop();
            }
            else if (mode == Microsoft.Win32.PowerModes.Resume)
            {
                _stopwatch.Restart();
                if (IsVisible)
                {
                    _timer.Start();
                }
            }
        }
        else
        {
            _window.Dispatcher.Invoke(() => HandlePowerModeChangedForTest(mode));
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!IsVisible || !_player.IsLoaded) return;

        double deltaSeconds = _stopwatch.Elapsed.TotalSeconds;
        _stopwatch.Restart();

        if (deltaSeconds > 0.2) deltaSeconds = 0.2; // Clamp delta to avoid large jump
        _accumulatedAnimationSeconds += deltaSeconds;

        var workArea = ScreenWorkspace.GetPrimaryWorkArea(_window.Handle);
        double speedPxPerSec = _movementSpeedDip * workArea.DipScale;

        _controller.Update(speedPxPerSec, deltaSeconds);

        var frame = _player.GetFrame(_controller.ActionName, _accumulatedAnimationSeconds, out _);
        _window.SetFrame(frame, _controller.FacingLeft);

        double widthDip = _player.MaxPixelWidth * (_scalePercent / 100.0);
        double heightDip = _player.MaxPixelHeight * (_scalePercent / 100.0);
        int widthPx = workArea.DipToPx(widthDip);
        int heightPx = workArea.DipToPx(heightDip);

        int posX = (int)Math.Round(_controller.CurrentX);
        int posY = (int)Math.Round(_controller.CurrentY);

        if (_window.Handle != nint.Zero)
        {
            PetWindowInterop.SetWindowPos(
                _window.Handle,
                nint.Zero,
                posX,
                posY,
                widthPx,
                heightPx,
                PetWindowInterop.SWP_NOACTIVATE | PetWindowInterop.SWP_NOZORDER
            );
        }
    }

    internal void SetPositionForTest(double x, double y)
    {
        _controller.Movement.CurrentX = x;
        _controller.Movement.CurrentY = y;
        _controller.Movement.TargetX = x;
        _controller.Movement.TargetY = y;

        if (_window.Handle != nint.Zero)
        {
            var workArea = ScreenWorkspace.GetPrimaryWorkArea(_window.Handle);
            double widthDip = _player.MaxPixelWidth * (_scalePercent / 100.0);
            double heightDip = _player.MaxPixelHeight * (_scalePercent / 100.0);
            int widthPx = workArea.DipToPx(widthDip);
            int heightPx = workArea.DipToPx(heightDip);

            PetWindowInterop.SetWindowPos(
                _window.Handle,
                nint.Zero,
                (int)Math.Round(x),
                (int)Math.Round(y),
                widthPx,
                heightPx,
                PetWindowInterop.SWP_NOACTIVATE | PetWindowInterop.SWP_NOZORDER
            );
        }
    }
}
