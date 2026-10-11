using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using VChibi.Services;

namespace VChibi.ViewModels;

public partial class PetFrameItemViewModel : ObservableObject
{
    [ObservableProperty]
    private int _index;

    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public BitmapSource Thumbnail { get; }
    public PetValidator.PngPreflightResult Preflight { get; }

    [ObservableProperty]
    private bool _isSelected;

    public PetFrameItemViewModel(string filePath, BitmapSource thumbnail, int index, PetValidator.PngPreflightResult preflight)
    {
        FilePath = filePath;
        Thumbnail = thumbnail;
        _index = index;
        Preflight = preflight;
    }
}
