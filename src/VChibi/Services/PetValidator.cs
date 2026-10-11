using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using VChibi.Models;
namespace VChibi.Services;

public class ValidationBudgets
{
    public long MaxManifestSizeBytes { get; init; } = PetValidator.MaxManifestSizeBytes;
    public long MaxPngFileSizeBytes { get; init; } = PetValidator.MaxPngFileSizeBytes;
    public long MaxTotalPngSizeBytes { get; init; } = PetValidator.MaxTotalPngSizeBytes;
    public long MaxPbgra32CacheBudget { get; init; } = PetValidator.MaxPbgra32CacheBudget;
    public long MaxNativePeakDecodeBudget { get; init; } = PetValidator.MaxNativePeakDecodeBudget;
}

public static class PetValidator
{
    public const long MaxManifestSizeBytes = 1024 * 1024; // 1 MiB
    public const long MaxPngFileSizeBytes = 32 * 1024 * 1024; // 32 MiB
    public const long MaxTotalPngSizeBytes = 128 * 1024 * 1024; // 128 MiB

    public const int MaxFramesPerAction = 256;
    public const int MaxDimensionPixels = 2048;

    public const long MaxPbgra32CacheBudget = 64 * 1024 * 1024; // 64 MiB
    public const long MaxNativePeakDecodeBudget = 128 * 1024 * 1024; // 128 MiB

    // Standard PNG 8-byte magic header: \x89PNG\r\n\x1a\n
    private static readonly byte[] PngMagicHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static readonly string[] AllowedActionNames = { "idle", "walk" };

    public static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').TrimStart('/');
    }

    public static bool HasReparsePointInPath(string fullPath, string rootDirectory)
    {
        try
        {
            var fullRoot = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = Path.GetFullPath(fullPath);

            while (!string.IsNullOrEmpty(current) && current.Length >= fullRoot.Length)
            {
                if (File.Exists(current) || Directory.Exists(current))
                {
                    var attr = File.GetAttributes(current);
                    if ((attr & FileAttributes.ReparsePoint) != 0)
                    {
                        return true;
                    }
                }

                if (string.Equals(current, fullRoot, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = Path.GetDirectoryName(current);
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    public static bool IsSafeRelativePath(string relativePath, string rootDirectory, out string fullCanonicalPath)
    {
        fullCanonicalPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath)) return false;

        // Reject absolute paths, drive letters, UNC paths
        if (Path.IsPathRooted(relativePath)) return false;
        if (relativePath.Contains(':') || relativePath.StartsWith("//") || relativePath.StartsWith("\\\\")) return false;

        var fullRoot = Path.GetFullPath(rootDirectory);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            fullRoot += Path.DirectorySeparatorChar;
        }

        try
        {
            var combined = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            // Check canonical boundary with separator
            if (!combined.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check for symlinks/reparse points
            if (HasReparsePointInPath(combined, rootDirectory))
            {
                return false;
            }

            fullCanonicalPath = combined;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static long CopyBoundedStream(string sourcePath, string destinationPath, long maxBytes)
    {
        var destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        var tempDest = destinationPath + ".tmp";
        try
        {
            long totalBytesRead = 0;
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destStream = new FileStream(tempDest, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920]; // 80 KiB buffer
                int bytesRead;
                while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    totalBytesRead += bytesRead;
                    if (totalBytesRead > maxBytes)
                    {
                        throw new InvalidDataException($"Dung lượng file '{Path.GetFileName(sourcePath)}' vượt quá giới hạn tối đa ({maxBytes} bytes).");
                    }
                    destStream.Write(buffer, 0, bytesRead);
                }
            }

            File.Move(tempDest, destinationPath, overwrite: true);
            return totalBytesRead;
        }
        catch
        {
            if (File.Exists(tempDest))
            {
                try { File.Delete(tempDest); } catch { }
            }
            throw;
        }
    }

    public static void ValidateManifestJson(string manifestPath, out PetDefinition definition, ValidationBudgets? budgets = null)
    {
        long maxManifestSize = budgets?.MaxManifestSizeBytes ?? MaxManifestSizeBytes;
        var fileInfo = new FileInfo(manifestPath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"Không tìm thấy file manifest: {manifestPath}");
        }

        if (fileInfo.Length > maxManifestSize)
        {
            throw new InvalidDataException($"File manifest vượt quá giới hạn ({fileInfo.Length} bytes).");
        }

        using var stream = File.OpenRead(manifestPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        definition = JsonSerializer.Deserialize<PetDefinition>(stream, options)
            ?? throw new InvalidDataException("Nội dung manifest pet.json không hợp lệ.");

        if (definition.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Phiên bản schema {definition.SchemaVersion} không được hỗ trợ (yêu cầu version 1).");
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new InvalidDataException("Tên nhân vật không được để trống.");
        }

        if (definition.Animations == null || definition.Animations.Count == 0)
        {
            throw new InvalidDataException("Gói pet không chứa bất kỳ animation nào.");
        }

        // Check for unknown or malicious animation action keys
        foreach (var key in definition.Animations.Keys)
        {
            if (!AllowedActionNames.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Tên hành động không hợp lệ: '{key}'. Chỉ chấp nhận 'idle' và 'walk'.");
            }
        }

        if (!definition.Animations.TryGetValue("idle", out var idleAnim) || idleAnim.Frames == null || idleAnim.Frames.Count == 0)
        {
            throw new InvalidDataException("Thiếu animation bắt buộc 'idle' hoặc 'idle' không có frame nào.");
        }

        if (!definition.Animations.TryGetValue("walk", out var walkAnim) || walkAnim.Frames == null || walkAnim.Frames.Count == 0)
        {
            throw new InvalidDataException("Thiếu animation bắt buộc 'walk' hoặc 'walk' không có frame nào.");
        }

        foreach (var (actionName, anim) in definition.Animations)
        {
            if (anim.Fps < 1 || anim.Fps > 60)
            {
                throw new InvalidDataException($"FPS cho hành động '{actionName}' phải trong khoảng 1–60 (hiện tại: {anim.Fps}).");
            }

            if (anim.Frames.Count > MaxFramesPerAction)
            {
                throw new InvalidDataException($"Số lượng frame cho hành động '{actionName}' vượt quá {MaxFramesPerAction} ({anim.Frames.Count} frames).");
            }
        }
    }

    public static void ValidatePackageAssets(string rootDirectory, PetDefinition definition, ValidationBudgets? budgets = null)
    {
        long maxPngSize = budgets?.MaxPngFileSizeBytes ?? MaxPngFileSizeBytes;
        long maxTotalSize = budgets?.MaxTotalPngSizeBytes ?? MaxTotalPngSizeBytes;
        long maxCacheBudget = budgets?.MaxPbgra32CacheBudget ?? MaxPbgra32CacheBudget;
        long maxNativeBudget = budgets?.MaxNativePeakDecodeBudget ?? MaxNativePeakDecodeBudget;

        long totalPngBytes = 0;
        long totalPbgra32Bytes = 0;
        long totalNativePeakBytes = 0;
        var allRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var anim in definition.Animations.Values)
        {
            foreach (var frame in anim.Frames)
            {
                allRelativePaths.Add(frame);
            }
        }

        foreach (var relativePath in allRelativePaths)
        {
            if (!IsSafeRelativePath(relativePath, rootDirectory, out var fullPath))
            {
                throw new InvalidDataException($"Đường dẫn frame không an toàn hoặc vượt ra ngoài thư mục gói: {relativePath}");
            }

            var fi = new FileInfo(fullPath);
            if (!fi.Exists)
            {
                throw new FileNotFoundException($"Không tìm thấy file frame: {relativePath}");
            }

            using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!TryPreflightPngStream(
                fileStream,
                relativePath,
                budgets,
                totalPngBytes,
                totalPbgra32Bytes,
                totalNativePeakBytes,
                out var preflight,
                out var preflightError))
            {
                throw new InvalidDataException(preflightError);
            }

            totalPngBytes += preflight.StreamLength;
            totalPbgra32Bytes += preflight.Pbgra32Bytes;
            totalNativePeakBytes += preflight.NativePeakBytes;

            // Sanity check that the file can be decoded without full caching
            try
            {
                var decoder = new PngBitmapDecoder(fileStream, BitmapCreateOptions.None, BitmapCacheOption.None);
                if (decoder.Frames.Count == 0)
                {
                    throw new InvalidDataException($"File PNG '{relativePath}' không chứa frame nào.");
                }
            }
            catch (Exception ex) when (ex is not InvalidDataException)
            {
                throw new InvalidDataException($"Không thể giải mã file PNG '{relativePath}': {ex.Message}", ex);
            }
        }

        if (totalPbgra32Bytes > maxCacheBudget)
        {
            throw new InvalidDataException($"Tổng bộ nhớ cache frame Pbgra32 ước tính ({totalPbgra32Bytes / (1024 * 1024)} MiB) vượt quá giới hạn ngân sách.");
        }

        if (totalNativePeakBytes > maxNativeBudget)
        {
            throw new InvalidDataException($"Ước tính bộ nhớ giải mã peak ({totalNativePeakBytes / (1024 * 1024)} MiB) vượt quá giới hạn ngân sách.");
        }
    }

    public readonly record struct PngPreflightResult(
        int Width,
        int Height,
        int BitDepth,
        int ColorType,
        long StreamLength,
        long Pbgra32Bytes,
        long NativePeakBytes
    );

    public static bool TryPreflightPngStream(
        Stream stream,
        string fileNameForErrors,
        ValidationBudgets? budgets,
        long currentTotalPngBytes,
        long currentTotalPbgra32Bytes,
        long currentTotalNativePeakBytes,
        out PngPreflightResult preflight,
        out string? errorMessage)
    {
        preflight = default;
        errorMessage = null;

        long maxPngSize = budgets?.MaxPngFileSizeBytes ?? MaxPngFileSizeBytes;
        long maxTotalSize = budgets?.MaxTotalPngSizeBytes ?? MaxTotalPngSizeBytes;
        long maxCacheBudget = budgets?.MaxPbgra32CacheBudget ?? MaxPbgra32CacheBudget;
        long maxNativeBudget = budgets?.MaxNativePeakDecodeBudget ?? MaxNativePeakDecodeBudget;

        long streamLength = stream.Length;
        if (streamLength > maxPngSize)
        {
            errorMessage = $"File PNG '{fileNameForErrors}' vượt quá giới hạn ({streamLength} bytes > {maxPngSize} bytes).";
            return false;
        }

        if (streamLength < 33)
        {
            errorMessage = $"File '{fileNameForErrors}' quá nhỏ ({streamLength} bytes), không phải định dạng PNG hợp lệ.";
            return false;
        }

        if (currentTotalPngBytes + streamLength > maxTotalSize)
        {
            errorMessage = $"Tổng dung lượng các file PNG ({currentTotalPngBytes + streamLength} bytes) vượt quá giới hạn ngân sách ({maxTotalSize} bytes).";
            return false;
        }

        Span<byte> header = stackalloc byte[33];
        stream.Position = 0;
        int readCount = stream.Read(header);
        if (readCount != 33)
        {
            errorMessage = $"Không thể đọc đầy đủ header PNG của file '{fileNameForErrors}'.";
            return false;
        }

        if (!header.Slice(0, 8).SequenceEqual(PngMagicHeader))
        {
            errorMessage = $"File '{fileNameForErrors}' không phải định dạng PNG hợp lệ (sai magic header).";
            return false;
        }

        uint ihdrLength = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(8, 4));
        if (ihdrLength != 13)
        {
            errorMessage = $"File '{fileNameForErrors}' không hợp lệ: độ dài IHDR chunk phải là 13 bytes (thực tế: {ihdrLength}).";
            return false;
        }

        // IHDR chunk type: 0x49, 0x48, 0x44, 0x52 ('I', 'H', 'D', 'R')
        if (header[12] != 0x49 || header[13] != 0x48 || header[14] != 0x44 || header[15] != 0x52)
        {
            errorMessage = $"File '{fileNameForErrors}' không hợp lệ: chunk đầu tiên phải là IHDR.";
            return false;
        }

        uint widthU = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(16, 4));
        uint heightU = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(20, 4));
        byte bitDepth = header[24];
        byte colorType = header[25];

        if (widthU == 0 || widthU > MaxDimensionPixels || heightU == 0 || heightU > MaxDimensionPixels)
        {
            errorMessage = $"Kích thước ảnh '{fileNameForErrors}' ({widthU}x{heightU}) không hợp lệ hoặc vượt quá {MaxDimensionPixels}x{MaxDimensionPixels} px.";
            return false;
        }

        int channels = colorType switch
        {
            0 => 1, // Grayscale
            2 => 3, // RGB
            3 => 1, // Indexed / palette
            4 => 2, // Grayscale with alpha
            6 => 4, // RGBA
            _ => 4  // Fallback
        };

        int bitsPerPixel = bitDepth * channels;
        if (bitsPerPixel <= 0) bitsPerPixel = 32;
        int bytesPerPixel = Math.Max(4, (bitsPerPixel + 7) / 8);

        long pbgraBytes = (long)widthU * heightU * 4;
        long nativePeakBytes = (long)widthU * heightU * bytesPerPixel * 2;

        if (currentTotalPbgra32Bytes + pbgraBytes > maxCacheBudget)
        {
            errorMessage = $"Tổng bộ nhớ cache frame Pbgra32 ước tính ({ (currentTotalPbgra32Bytes + pbgraBytes) / (1024 * 1024) } MiB) vượt quá giới hạn ngân sách ({ maxCacheBudget / (1024 * 1024) } MiB).";
            return false;
        }

        if (currentTotalNativePeakBytes + nativePeakBytes > maxNativeBudget)
        {
            errorMessage = $"Ước tính bộ nhớ giải mã peak ({ (currentTotalNativePeakBytes + nativePeakBytes) / (1024 * 1024) } MiB) vượt quá giới hạn ngân sách ({ maxNativeBudget / (1024 * 1024) } MiB).";
            return false;
        }

        stream.Position = 0;
        preflight = new PngPreflightResult((int)widthU, (int)heightU, bitDepth, colorType, streamLength, pbgraBytes, nativePeakBytes);
        return true;
    }

    public static bool TryValidateAndDecodePng(
        string filePath,
        ValidationBudgets? budgets,
        long currentTotalPngBytes,
        long currentTotalPbgra32Bytes,
        long currentTotalNativePeakBytes,
        out BitmapSource? bitmap,
        out PngPreflightResult preflight,
        out string? errorMessage)
    {
        bitmap = null;
        preflight = default;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            errorMessage = $"Không tìm thấy file: {filePath}";
            return false;
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!TryPreflightPngStream(
                fileStream,
                Path.GetFileName(filePath),
                budgets,
                currentTotalPngBytes,
                currentTotalPbgra32Bytes,
                currentTotalNativePeakBytes,
                out preflight,
                out errorMessage))
            {
                return false;
            }

            // Preflight passed: decode safely without risk of memory bomb
            var decoder = new PngBitmapDecoder(fileStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                errorMessage = $"File PNG '{Path.GetFileName(filePath)}' không chứa frame nào.";
                return false;
            }

            var frame = decoder.Frames[0];
            var converted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Pbgra32, null, 0);
            converted.Freeze();

            bitmap = converted;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Không thể giải mã file PNG '{Path.GetFileName(filePath)}': {ex.Message}";
            return false;
        }
    }
}
