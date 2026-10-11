using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using VChibi.Platform.Windows;

namespace VChibi.Views;

public partial class PetWindow : Window
{
    private nint _hwnd = nint.Zero;

    public nint Handle
    {
        get
        {
            if (_hwnd == nint.Zero)
            {
                var helper = new WindowInteropHelper(this);
                _hwnd = helper.EnsureHandle();
            }
            return _hwnd;
        }
    }
    public double FlipScaleX => FlipTransform.ScaleX;
    public bool IsFacingLeft => FlipTransform.ScaleX < 0;

    public PetWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        _hwnd = helper.Handle;

        // Apply native Win32 layered styles: WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
        PetWindowInterop.EnableClickThroughAndNoActivate(_hwnd);
    }

    public void SetFrame(BitmapSource? frame, bool facingLeft)
    {
        if (frame == null) return;

        SpriteImage.Source = frame;

        if (facingLeft)
        {
            FlipTransform.ScaleX = -1;
            FlipTransform.CenterX = frame.PixelWidth / 2.0;
        }
        else
        {
            FlipTransform.ScaleX = 1;
            FlipTransform.CenterX = 0;
        }
    }

    public void SetDimensions(double widthDip, double heightDip)
    {
        Width = Math.Max(16, widthDip);
        Height = Math.Max(16, heightDip);
        RootGrid.Width = Width;
        RootGrid.Height = Height;
    }

    public void ClearFrame()
    {
        SpriteImage.Source = null;
    }
}
