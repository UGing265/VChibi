using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using VChibi.Models;

namespace VChibi.ViewModels;

public partial class PetListItemViewModel : ObservableObject
{
    public PetDefinition Definition { get; }

    public string Id => Definition.Id;
    public string Name => Definition.Name;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private ImageSource? _thumbnail;

    public PetListItemViewModel(PetDefinition definition, bool isActive = false)
    {
        Definition = definition;
        _isActive = isActive;
        LoadThumbnail();
    }

    private void LoadThumbnail()
    {
        try
        {
            if (Definition.Animations.TryGetValue("idle", out var idleAnim) &&
                idleAnim.Frames.Count > 0)
            {
                var relPath = idleAnim.Frames[0];
                var fullPath = System.IO.Path.Combine(Definition.RootDirectory, relPath);
                if (System.IO.File.Exists(fullPath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    Thumbnail = bmp;
                }
            }
        }
        catch
        {
            Thumbnail = null;
        }
    }
}
