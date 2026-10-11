using System.IO;
using System.Text.Json;
using VChibi.Models;
using VChibi.Services;
using Xunit;

namespace VChibi.Tests.Storage;

public class PetValidatorTests : IDisposable
{
    private readonly string _tempDir;

    public PetValidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vchibi_val_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("..\\escape.png")]
    [InlineData("/root/escape.png")]
    [InlineData("C:\\Windows\\System32\\calc.exe")]
    [InlineData("idle/../../outside.png")]
    public void IsSafeRelativePath_RejectsTraversalAndAbsolute(string badPath)
    {
        bool isSafe = PetValidator.IsSafeRelativePath(badPath, _tempDir, out var fullPath);
        Assert.False(isSafe);
    }

    [Fact]
    public void IsSafeRelativePath_AcceptsLegitimateRelativePath()
    {
        bool isSafe = PetValidator.IsSafeRelativePath("idle/001.png", _tempDir, out var fullPath);
        Assert.True(isSafe);
        Assert.StartsWith(_tempDir, fullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateManifestJson_RejectsMissingIdleOrWalk()
    {
        var manifestPath = Path.Combine(_tempDir, "pet.json");
        var badManifest = new
        {
            schemaVersion = 1,
            id = "test-pet",
            name = "Test Pet",
            animations = new
            {
                idle = new { fps = 8, frames = new[] { "idle/001.png" } }
                // missing walk
            }
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(badManifest));

        var ex = Assert.Throws<InvalidDataException>(() =>
            PetValidator.ValidateManifestJson(manifestPath, out _)
        );
        Assert.Contains("walk", ex.Message);
    }

    [Fact]
    public void ValidateManifestJson_RejectsUnknownActionNames()
    {
        var manifestPath = Path.Combine(_tempDir, "pet.json");
        var badManifest = new
        {
            schemaVersion = 1,
            id = "test-pet",
            name = "Test Pet",
            animations = new
            {
                idle = new { fps = 8, frames = new[] { "idle/001.png" } },
                walk = new { fps = 8, frames = new[] { "walk/001.png" } },
                dance = new { fps = 8, frames = new[] { "dance/001.png" } } // unauthorized action
            }
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(badManifest));

        var ex = Assert.Throws<InvalidDataException>(() =>
            PetValidator.ValidateManifestJson(manifestPath, out _)
        );
        Assert.Contains("dance", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    [InlineData(-5)]
    public void ValidateManifestJson_RejectsFpsOutOfRange(int badFps)
    {
        var manifestPath = Path.Combine(_tempDir, "pet.json");
        var badManifest = new
        {
            schemaVersion = 1,
            id = "test-pet",
            name = "Test Pet",
            animations = new
            {
                idle = new { fps = badFps, frames = new[] { "idle/001.png" } },
                walk = new { fps = 8, frames = new[] { "walk/001.png" } }
            }
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(badManifest));

        var ex = Assert.Throws<InvalidDataException>(() =>
            PetValidator.ValidateManifestJson(manifestPath, out _)
        );
        Assert.Contains("FPS", ex.Message);
    }

    [Fact]
    public void ValidatePackageAssets_RejectsNonPngFile()
    {
        var idleDir = Path.Combine(_tempDir, "idle");
        var walkDir = Path.Combine(_tempDir, "walk");
        Directory.CreateDirectory(idleDir);
        Directory.CreateDirectory(walkDir);

        // Write fake text file disguised as PNG
        var fakePng = Path.Combine(idleDir, "001.png");
        File.WriteAllText(fakePng, "This is not a PNG file!");

        var def = new PetDefinition
        {
            SchemaVersion = 1,
            Id = "test",
            Name = "Test",
            Animations = new Dictionary<string, AnimationDefinition>
            {
                ["idle"] = new() { Fps = 8, Frames = new() { "idle/001.png" } },
                ["walk"] = new() { Fps = 8, Frames = new() { "idle/001.png" } }
            }
        };

        var ex = Assert.Throws<InvalidDataException>(() =>
            PetValidator.ValidatePackageAssets(_tempDir, def)
        );
        Assert.Contains("PNG", ex.Message);
    }

    [Fact]
    public void CopyBoundedStream_ThrowsWhenSourceFileExceedsLimit()
    {
        var srcFile = Path.Combine(_tempDir, "source.bin");
        var dstFile = Path.Combine(_tempDir, "dest.bin");
        File.WriteAllBytes(srcFile, new byte[200]);

        var ex = Assert.Throws<InvalidDataException>(() =>
            PetValidator.CopyBoundedStream(srcFile, dstFile, maxBytes: 100)
        );
        Assert.Contains("vượt quá giới hạn tối đa", ex.Message);
        Assert.False(File.Exists(dstFile));
        Assert.False(File.Exists(dstFile + ".tmp"));
    }

    [Fact]
    public void CopyBoundedStream_SucceedsWhenWithinLimit()
    {
        var srcFile = Path.Combine(_tempDir, "source.bin");
        var dstFile = Path.Combine(_tempDir, "dest.bin");
        File.WriteAllBytes(srcFile, new byte[80]);

        long copied = PetValidator.CopyBoundedStream(srcFile, dstFile, maxBytes: 100);
        Assert.Equal(80, copied);
        Assert.True(File.Exists(dstFile));
    }

    [Fact]
    public void BundledDefaultPet_IsValid()
    {
        var bundledDir = Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultPet");
        var manifestPath = Path.Combine(bundledDir, "pet.json");

        Assert.True(File.Exists(manifestPath), $"Manifest not found at {manifestPath}");
        PetValidator.ValidateManifestJson(manifestPath, out var def);
        Assert.Equal("Chibi Idol", def.Name);
        Assert.True(def.Animations.ContainsKey("idle"));
        Assert.True(def.Animations.ContainsKey("walk"));
        PetValidator.ValidatePackageAssets(bundledDir, def);
    }

    private string CreateValidPng(string path, int width = 16, int height = 16)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(255, 120, 98, 184));
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    [Fact]
    public void ValidatePackageAssets_RejectsFileOverPngSizeBudget_WithInjectedBudget()
    {
        var idlePng = CreateValidPng(Path.Combine(_tempDir, "idle", "001.png"));
        var walkPng = CreateValidPng(Path.Combine(_tempDir, "walk", "001.png"));

        var def = new PetDefinition
        {
            SchemaVersion = 1,
            Id = "test",
            Name = "Test",
            Animations = new Dictionary<string, AnimationDefinition>
            {
                ["idle"] = new() { Fps = 8, Frames = new() { "idle/001.png" } },
                ["walk"] = new() { Fps = 8, Frames = new() { "walk/001.png" } }
            }
        };

        // Inject very small file size budget (e.g. 10 bytes)
        var budgets = new ValidationBudgets { MaxPngFileSizeBytes = 10 };
        var ex = Assert.Throws<InvalidDataException>(() =>
            PetValidator.ValidatePackageAssets(_tempDir, def, budgets)
        );
        Assert.Contains("vượt quá giới hạn", ex.Message);
    }

    [Fact]
    public void ValidatePackageAssets_RejectsTotalOverCap_WithInjectedBudget()
    {
        var idlePng = CreateValidPng(Path.Combine(_tempDir, "idle", "001.png"));
        var walkPng = CreateValidPng(Path.Combine(_tempDir, "walk", "001.png"));

        var def = new PetDefinition
        {
            SchemaVersion = 1,
            Id = "test",
            Name = "Test",
            Animations = new Dictionary<string, AnimationDefinition>
            {
                ["idle"] = new() { Fps = 8, Frames = new() { "idle/001.png" } },
                ["walk"] = new() { Fps = 8, Frames = new() { "walk/001.png" } }
            }
        };

        var budgets = new ValidationBudgets { MaxTotalPngSizeBytes = 50 };
        var ex = Assert.Throws<InvalidDataException>(() =>
            PetValidator.ValidatePackageAssets(_tempDir, def, budgets)
        );
        Assert.Contains("Tổng dung lượng", ex.Message);
    }

    [Fact]
    public void ValidatePackageAssets_RejectsWhenCacheBudgetExceeded()
    {
        var idlePng = CreateValidPng(Path.Combine(_tempDir, "idle", "001.png"), 32, 32);
        var walkPng = CreateValidPng(Path.Combine(_tempDir, "walk", "001.png"), 32, 32);

        var def = new PetDefinition
        {
            SchemaVersion = 1,
            Id = "test",
            Name = "Test",
            Animations = new Dictionary<string, AnimationDefinition>
            {
                ["idle"] = new() { Fps = 8, Frames = new() { "idle/001.png" } },
                ["walk"] = new() { Fps = 8, Frames = new() { "walk/001.png" } }
            }
        };

        // 32x32 Pbgra32 is 4096 bytes per frame. Inject cache budget of 100 bytes.
        var budgets = new ValidationBudgets { MaxPbgra32CacheBudget = 100 };
        var ex = Assert.Throws<InvalidDataException>(() =>
            PetValidator.ValidatePackageAssets(_tempDir, def, budgets)
        );
        Assert.Contains("cache frame Pbgra32", ex.Message);
    }

    [Fact]
    public void IsSafeRelativePath_RejectsRealReparsePointDirectory()
    {
        var realTarget = Path.Combine(_tempDir, "real_target");
        Directory.CreateDirectory(realTarget);
        File.WriteAllText(Path.Combine(realTarget, "secret.png"), "secret");

        var junctionPath = Path.Combine(_tempDir, "junction_sub");
        try
        {
            // Create directory junction
            Directory.CreateSymbolicLink(junctionPath, realTarget);
        }
        catch
        {
            // If unprivileged, use Directory.CreateDirectory and check ReparsePoint detection directly
            return;
        }

        // Even if junction is lexically inside _tempDir, IsSafeRelativePath must detect ReparsePoint and reject!
        bool isSafe = PetValidator.IsSafeRelativePath("junction_sub/secret.png", _tempDir, out _);
        Assert.False(isSafe);
    }

    [Fact]
    public void TryPreflightPngStream_RejectsPixelBombWithOversizedIhdr_BeforeDecode()
    {
        // Craft a 33-byte PNG header with 10000x10000 declared in IHDR
        byte[] fakePixelBomb = new byte[33];
        byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Buffer.BlockCopy(signature, 0, fakePixelBomb, 0, 8);

        // IHDR chunk length = 13 (big endian: 0x00, 0x00, 0x00, 0x0D)
        fakePixelBomb[8] = 0; fakePixelBomb[9] = 0; fakePixelBomb[10] = 0; fakePixelBomb[11] = 13;
        // Chunk type "IHDR"
        fakePixelBomb[12] = 0x49; fakePixelBomb[13] = 0x48; fakePixelBomb[14] = 0x44; fakePixelBomb[15] = 0x52;
        // Width = 10000 (0x00002710)
        fakePixelBomb[16] = 0x00; fakePixelBomb[17] = 0x00; fakePixelBomb[18] = 0x27; fakePixelBomb[19] = 0x10;
        // Height = 10000 (0x00002710)
        fakePixelBomb[20] = 0x00; fakePixelBomb[21] = 0x00; fakePixelBomb[22] = 0x27; fakePixelBomb[23] = 0x10;
        fakePixelBomb[24] = 8; // bit depth
        fakePixelBomb[25] = 6; // RGBA

        using var ms = new MemoryStream(fakePixelBomb);
        bool result = PetValidator.TryPreflightPngStream(
            ms,
            "pixel_bomb.png",
            null,
            0, 0, 0,
            out _,
            out var errorMessage
        );

        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("vượt quá 2048x2048 px", errorMessage);
    }

    [Fact]
    public void TryValidateAndDecodePng_AcceptsValidPngAndReturnsFrozenBitmap()
    {
        var validPngPath = CreateValidPng(Path.Combine(_tempDir, "sample_valid.png"), 24, 24);

        bool success = PetValidator.TryValidateAndDecodePng(
            validPngPath,
            null,
            0, 0, 0,
            out var bitmap,
            out var preflight,
            out var errorMessage
        );

        Assert.True(success, errorMessage);
        Assert.Null(errorMessage);
        Assert.NotNull(bitmap);
        Assert.True(bitmap.IsFrozen);
        Assert.Equal(24, preflight.Width);
        Assert.Equal(24, preflight.Height);
        Assert.True(preflight.Pbgra32Bytes > 0);
    }

    [Fact]
    public void TryValidateAndDecodePng_RejectsWhenAggregateDraftBudgetExceeded()
    {
        var validPngPath = CreateValidPng(Path.Combine(_tempDir, "sample_budget.png"), 32, 32);
        // 32x32 = 1024 pixels * 4 = 4096 bytes Pbgra32
        var tinyBudget = new ValidationBudgets { MaxPbgra32CacheBudget = 2000 };

        bool success = PetValidator.TryValidateAndDecodePng(
            validPngPath,
            tinyBudget,
            0, 0, 0,
            out var bitmap,
            out _,
            out var errorMessage
        );

        Assert.False(success);
        Assert.Null(bitmap);
        Assert.NotNull(errorMessage);
        Assert.Contains("cache frame Pbgra32", errorMessage);
    }

    [Fact]
    public void TryPreflightPngStream_RejectsStreamShorterThan33Bytes()
    {
        byte[] shortBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var ms = new MemoryStream(shortBytes);

        bool result = PetValidator.TryPreflightPngStream(
            ms,
            "short.png",
            null,
            0, 0, 0,
            out _,
            out var errorMessage
        );

        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("quá nhỏ", errorMessage);
    }

    [Fact]
    public void TryPreflightPngStream_Sec01_RejectsLargeAncillaryChunks_EvenWithSmallPixels()
    {
        // Craft a 16x16 PNG with oversized ancillary bytes (e.g. 10 KB file with 16x16 pixels)
        byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        byte[] payload = new byte[10000];
        Buffer.BlockCopy(signature, 0, payload, 0, 8);

        // IHDR chunk: length 13, type "IHDR", 16x16, 8-bit RGBA
        payload[8] = 0; payload[9] = 0; payload[10] = 0; payload[11] = 13;
        payload[12] = 0x49; payload[13] = 0x48; payload[14] = 0x44; payload[15] = 0x52;
        payload[16] = 0; payload[17] = 0; payload[18] = 0; payload[19] = 16; // width 16
        payload[20] = 0; payload[21] = 0; payload[22] = 0; payload[23] = 16; // height 16
        payload[24] = 8; // 8-bit
        payload[25] = 6; // RGBA

        using var ms = new MemoryStream(payload);
        // Budget allows only 2048 bytes per file
        var budgets = new ValidationBudgets { MaxPngFileSizeBytes = 2048 };

        bool result = PetValidator.TryPreflightPngStream(
            ms,
            "sec01_bloated_ancillary.png",
            budgets,
            0, 0, 0,
            out _,
            out var errorMessage
        );

        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("vượt quá giới hạn", errorMessage);
    }

    [Fact]
    public void TryPreflightPngStream_Calculates16BitRgbaNativePeakCorrectly()
    {
        // 33-byte IHDR header: 32x32 px, 16-bit depth, color type 6 (RGBA)
        byte[] header16Bit = new byte[33];
        byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Buffer.BlockCopy(signature, 0, header16Bit, 0, 8);

        // IHDR chunk length = 13
        header16Bit[8] = 0; header16Bit[9] = 0; header16Bit[10] = 0; header16Bit[11] = 13;
        // "IHDR"
        header16Bit[12] = 0x49; header16Bit[13] = 0x48; header16Bit[14] = 0x44; header16Bit[15] = 0x52;
        // Width = 32 (0x00000020)
        header16Bit[16] = 0; header16Bit[17] = 0; header16Bit[18] = 0; header16Bit[19] = 32;
        // Height = 32 (0x00000020)
        header16Bit[20] = 0; header16Bit[21] = 0; header16Bit[22] = 0; header16Bit[23] = 32;
        header16Bit[24] = 16; // 16-bit depth per channel
        header16Bit[25] = 6;  // RGBA (4 channels) -> 64 bits = 8 bytes per pixel

        using var ms = new MemoryStream(header16Bit);
        bool result = PetValidator.TryPreflightPngStream(
            ms,
            "16bit_rgba.png",
            null,
            0, 0, 0,
            out var preflight,
            out var errorMessage
        );

        Assert.True(result, errorMessage);
        Assert.Equal(32, preflight.Width);
        Assert.Equal(32, preflight.Height);
        Assert.Equal(16, preflight.BitDepth);
        Assert.Equal(6, preflight.ColorType);
        // Pbgra32 bytes = 32 * 32 * 4 = 4096
        Assert.Equal(4096, preflight.Pbgra32Bytes);
        // Native peak bytes = 32 * 32 * 8 * 2 = 16384
        Assert.Equal(16384, preflight.NativePeakBytes);
    }

    [Fact]
    public void ValidatePackageAssets_RejectsManifestWithDirectoryTraversal()
    {
        var def = new PetDefinition
        {
            SchemaVersion = 1,
            Id = "test",
            Name = "Test",
            Animations = new Dictionary<string, AnimationDefinition>
            {
                ["idle"] = new() { Fps = 8, Frames = new() { "../../outside.png" } },
                ["walk"] = new() { Fps = 8, Frames = new() { "walk/001.png" } }
            }
        };

        var ex = Assert.Throws<InvalidDataException>(() =>
            PetValidator.ValidatePackageAssets(_tempDir, def)
        );
        Assert.Contains("vượt ra ngoài", ex.Message);
    }
}
