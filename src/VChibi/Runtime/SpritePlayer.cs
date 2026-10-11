using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VChibi.Models;
using VChibi.Services;

namespace VChibi.Runtime;

public class SpritePlayer
{
    private readonly Dictionary<string, (int Fps, List<BitmapSource> Frames)> _animations = new(StringComparer.OrdinalIgnoreCase);

    public int MaxPixelWidth { get; private set; }
    public int MaxPixelHeight { get; private set; }

    public bool IsLoaded => _animations.Count > 0;

    public static SpritePlayer LoadFromDefinition(PetDefinition definition, string rootDirectory)
    {
        var player = new SpritePlayer();
        player.Load(definition, rootDirectory);
        return player;
    }

    public void Load(PetDefinition definition, string rootDirectory)
    {
        _animations.Clear();
        int maxWidth = 0;
        int maxHeight = 0;

        foreach (var (actionName, anim) in definition.Animations)
        {
            var framesList = new List<BitmapSource>();
            foreach (var relPath in anim.Frames)
            {
                if (!PetValidator.IsSafeRelativePath(relPath, rootDirectory, out var fullPath))
                {
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    continue;
                }

                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0)
                {
                    var sourceFrame = decoder.Frames[0];
                    // Convert to standard Pbgra32 for optimal WPF rendering
                    var converted = new FormatConvertedBitmap(sourceFrame, PixelFormats.Pbgra32, null, 0);
                    converted.Freeze();

                    if (converted.PixelWidth > maxWidth) maxWidth = converted.PixelWidth;
                    if (converted.PixelHeight > maxHeight) maxHeight = converted.PixelHeight;

                    framesList.Add(converted);
                }
            }

            if (framesList.Count > 0)
            {
                _animations[actionName] = (Math.Max(1, anim.Fps), framesList);
            }
        }

        MaxPixelWidth = Math.Max(1, maxWidth);
        MaxPixelHeight = Math.Max(1, maxHeight);
    }

    public void SetSingleAction(int fps, IReadOnlyList<BitmapSource> frames)
    {
        _animations.Clear();
        var fList = frames.ToList();
        int maxWidth = 0;
        int maxHeight = 0;
        foreach (var frame in fList)
        {
            if (frame.PixelWidth > maxWidth) maxWidth = frame.PixelWidth;
            if (frame.PixelHeight > maxHeight) maxHeight = frame.PixelHeight;
        }
        _animations["idle"] = (Math.Max(1, fps), fList);
        MaxPixelWidth = Math.Max(1, maxWidth);
        MaxPixelHeight = Math.Max(1, maxHeight);
    }

    public void UpdateFps(string actionName, int fps)
    {
        if (_animations.TryGetValue(actionName, out var animData))
        {
            _animations[actionName] = (Math.Max(1, fps), animData.Frames);
        }
    }

    public BitmapSource? GetFrame(string actionName, double elapsedSeconds, out int frameIndex)
    {
        frameIndex = 0;
        if (!_animations.TryGetValue(actionName, out var animData) || animData.Frames.Count == 0)
        {
            if (!_animations.TryGetValue("idle", out animData) || animData.Frames.Count == 0)
            {
                return null;
            }
        }

        int fps = animData.Fps;
        int count = animData.Frames.Count;
        frameIndex = (int)(elapsedSeconds * fps) % count;
        if (frameIndex < 0) frameIndex = 0;

        return animData.Frames[frameIndex];
    }
}
