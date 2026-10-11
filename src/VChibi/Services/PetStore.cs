using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using VChibi.Models;

namespace VChibi.Services;

public record PetPackageItem(
    PetDefinition? Definition,
    string DirectoryPath,
    bool IsCorrupt,
    string? ErrorMessage
);

public class PetStore
{
    private static readonly Regex SafeIdRegex = new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);

    private readonly AppPaths _paths;
    private readonly AppLock _appLock;
    private readonly object _writeLock = new();

    public PetStore(AppPaths paths, AppLock appLock)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _appLock = appLock ?? throw new ArgumentNullException(nameof(appLock));

        if (!_appLock.IsAcquired)
        {
            throw new InvalidOperationException("Không thể khởi tạo PetStore khi chưa sở hữu AppLock (khóa độc quyền data root).");
        }

        _paths.EnsureDirectoriesCreated();
        RecoverPendingTransactions();
    }

    public static bool IsValidPetId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return SafeIdRegex.IsMatch(id);
    }

    public string GetSafePetDirectory(string petId)
    {
        if (!IsValidPetId(petId))
        {
            throw new ArgumentException($"Mã định danh pet không hợp lệ: '{petId}'. Chỉ chấp nhận chữ, số, gạch ngang và gạch dưới (1-64 ký tự).", nameof(petId));
        }

        var targetPath = Path.GetFullPath(Path.Combine(_paths.PetsDirectory, petId));
        var fullRoot = Path.GetFullPath(_paths.PetsDirectory);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            fullRoot += Path.DirectorySeparatorChar;
        }

        if (!targetPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Đường dẫn pet vượt ra ngoài thư mục lưu trữ: '{petId}'");
        }

        return targetPath;
    }

    private static bool IsPackageCompletelyValid(string targetDir, string expectedId)
    {
        var targetManifest = Path.Combine(targetDir, "pet.json");
        if (!File.Exists(targetManifest)) return false;
        try
        {
            PetValidator.ValidateManifestJson(targetManifest, out var def);
            if (!string.Equals(def.Id, expectedId, StringComparison.OrdinalIgnoreCase)) return false;
            PetValidator.ValidatePackageAssets(targetDir, def);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void RecoverPendingTransactions()
    {
        if (!_appLock.IsAcquired)
        {
            throw new InvalidOperationException("Không thể chạy phục hồi giao dịch khi chưa sở hữu AppLock.");
        }

        lock (_writeLock)
        {
            if (!Directory.Exists(_paths.StagingDirectory)) return;

            foreach (var dir in Directory.EnumerateDirectories(_paths.StagingDirectory))
            {
                var folderName = Path.GetFileName(dir);
                if (folderName.StartsWith("backup-", StringComparison.OrdinalIgnoreCase))
                {
                    var petId = folderName["backup-".Length..];
                    if (IsValidPetId(petId))
                    {
                        var targetDir = GetSafePetDirectory(petId);
                        bool targetValid = IsPackageCompletelyValid(targetDir, petId);

                        if (!targetValid)
                        {
                            if (Directory.Exists(targetDir))
                            {
                                try { Directory.Delete(targetDir, true); } catch { }
                            }
                            try
                            {
                                Directory.Move(dir, targetDir);
                                targetValid = IsPackageCompletelyValid(targetDir, petId);
                            }
                            catch
                            {
                                // Move failed: RETAIN backup directory so data is never lost!
                                continue;
                            }
                        }

                        // ONLY delete backup directory if target is confirmed completely valid!
                        if (targetValid && Directory.Exists(dir))
                        {
                            try { Directory.Delete(dir, true); } catch { }
                        }
                    }
                }
                else if (folderName.StartsWith("draft-", StringComparison.OrdinalIgnoreCase) ||
                         folderName.StartsWith("import-", StringComparison.OrdinalIgnoreCase))
                {
                    // Clean up orphaned uncommitted staging drafts
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
        }
    }

    public IReadOnlyList<PetPackageItem> GetAllPackages()
    {
        var items = new List<PetPackageItem>();
        if (!Directory.Exists(_paths.PetsDirectory))
        {
            return items;
        }

        foreach (var dir in Directory.EnumerateDirectories(_paths.PetsDirectory))
        {
            var folderName = Path.GetFileName(dir);
            if (!IsValidPetId(folderName))
            {
                items.Add(new PetPackageItem(null, dir, true, $"Tên thư mục gói không hợp lệ: '{folderName}'."));
                continue;
            }

            var manifestPath = Path.Combine(dir, "pet.json");
            if (!File.Exists(manifestPath))
            {
                items.Add(new PetPackageItem(null, dir, true, "Thiếu file manifest pet.json trong gói."));
                continue;
            }

            try
            {
                PetValidator.ValidateManifestJson(manifestPath, out var def);
                if (!string.Equals(def.Id, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new PetPackageItem(null, dir, true, $"ID trong manifest ('{def.Id}') không khớp với tên thư mục ('{folderName}')."));
                    continue;
                }

                PetValidator.ValidatePackageAssets(dir, def);
                def.RootDirectory = dir;
                items.Add(new PetPackageItem(def, dir, false, null));
            }
            catch (Exception ex)
            {
                items.Add(new PetPackageItem(null, dir, true, $"Gói bị hỏng: {ex.Message}"));
            }
        }

        return items.OrderBy(i => i.Definition?.Name ?? Path.GetFileName(i.DirectoryPath), StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<PetDefinition> GetAllPets()
    {
        return GetAllPackages()
            .Where(p => !p.IsCorrupt && p.Definition != null)
            .Select(p => p.Definition!)
            .ToList();
    }

    public PetDefinition? GetPet(string id)
    {
        if (!IsValidPetId(id)) return null;

        var dir = GetSafePetDirectory(id);
        var manifestPath = Path.Combine(dir, "pet.json");
        if (!File.Exists(manifestPath)) return null;

        try
        {
            PetValidator.ValidateManifestJson(manifestPath, out var def);
            if (!string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            PetValidator.ValidatePackageAssets(dir, def);
            def.RootDirectory = dir;
            return def;
        }
        catch
        {
            return null;
        }
    }

    public PetDefinition ImportPackage(string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Thư mục nguồn không tồn tại: {sourceDirectory}");
        }

        var sourceManifestPath = Path.Combine(sourceDirectory, "pet.json");
        if (!File.Exists(sourceManifestPath))
        {
            throw new FileNotFoundException($"Gói không chứa file cấu hình pet.json: {sourceDirectory}");
        }

        // Pre-validate source manifest
        PetValidator.ValidateManifestJson(sourceManifestPath, out var sourceDef);
        PetValidator.ValidatePackageAssets(sourceDirectory, sourceDef);

        lock (_writeLock)
        {
            var newId = $"pet-{Guid.NewGuid():N}";
            var stagingDir = Path.Combine(_paths.StagingDirectory, $"import-{newId}");
            var targetDir = GetSafePetDirectory(newId);

            try
            {
                if (Directory.Exists(stagingDir))
                {
                    Directory.Delete(stagingDir, true);
                }
                Directory.CreateDirectory(stagingDir);

                var stagedDef = new PetDefinition
                {
                    SchemaVersion = 1,
                    Id = newId,
                    Name = sourceDef.Name,
                    Animations = new Dictionary<string, AnimationDefinition>()
                };

                long totalStagedPngBytes = 0;

                foreach (var (actionName, anim) in sourceDef.Animations)
                {
                    var safeAction = actionName.Trim().ToLowerInvariant();
                    if (!PetValidator.AllowedActionNames.Contains(safeAction))
                    {
                        throw new InvalidDataException($"Tên hành động không hợp lệ: '{actionName}'. Chỉ chấp nhận 'idle' và 'walk'.");
                    }

                    var stagedAnim = new AnimationDefinition
                    {
                        Fps = anim.Fps,
                        Frames = new List<string>()
                    };

                    var actionDir = Path.Combine(stagingDir, safeAction);
                    Directory.CreateDirectory(actionDir);

                    for (int i = 0; i < anim.Frames.Count; i++)
                    {
                        var srcRel = anim.Frames[i];
                        PetValidator.IsSafeRelativePath(srcRel, sourceDirectory, out var srcFull);

                        var ext = Path.GetExtension(srcRel);
                        var frameName = $"{i + 1:D3}{ext}";
                        var dstRel = $"{safeAction}/{frameName}";
                        var dstFull = Path.Combine(stagingDir, safeAction, frameName);

                        long remainingBudget = PetValidator.MaxTotalPngSizeBytes - totalStagedPngBytes;
                        long fileMax = Math.Min(PetValidator.MaxPngFileSizeBytes, remainingBudget);

                        long copiedBytes = PetValidator.CopyBoundedStream(srcFull, dstFull, fileMax);
                        totalStagedPngBytes += copiedBytes;

                        stagedAnim.Frames.Add(dstRel);
                    }

                    stagedDef.Animations[safeAction] = stagedAnim;
                }

                // Validate staged snapshot
                PetValidator.ValidatePackageAssets(stagingDir, stagedDef);

                // Write final manifest with assigned id
                var stagingManifest = Path.Combine(stagingDir, "pet.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(stagedDef, options);
                File.WriteAllText(stagingManifest, json);

                // Commit: Move staging to destination
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }
                Directory.Move(stagingDir, targetDir);

                stagedDef.RootDirectory = targetDir;
                return stagedDef;
            }
            catch
            {
                if (Directory.Exists(stagingDir))
                {
                    try { Directory.Delete(stagingDir, true); } catch { }
                }
                throw;
            }
        }
    }

    public PetDefinition SavePetDraft(
        string? existingId,
        string name,
        Dictionary<string, (int Fps, IReadOnlyList<string> SourceFramePaths)> actions)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tên nhân vật không được để trống.", nameof(name));
        }

        if (!string.IsNullOrEmpty(existingId) && !IsValidPetId(existingId))
        {
            throw new ArgumentException($"Mã định danh pet không hợp lệ: '{existingId}'", nameof(existingId));
        }

        if (!actions.ContainsKey("idle") || actions["idle"].SourceFramePaths.Count == 0)
        {
            throw new ArgumentException("Thiếu animation 'idle' hoặc 'idle' không có frame nào.");
        }

        if (!actions.ContainsKey("walk") || actions["walk"].SourceFramePaths.Count == 0)
        {
            throw new ArgumentException("Thiếu animation 'walk' hoặc 'walk' không có frame nào.");
        }

        lock (_writeLock)
        {
            var petId = !string.IsNullOrWhiteSpace(existingId) ? existingId : $"pet-{Guid.NewGuid():N}";
            var stagingDir = Path.Combine(_paths.StagingDirectory, $"draft-{petId}");
            var targetDir = GetSafePetDirectory(petId);
            var backupDir = Path.Combine(_paths.StagingDirectory, $"backup-{petId}");

            try
            {
                if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);
                Directory.CreateDirectory(stagingDir);

                var committedDef = new PetDefinition
                {
                    SchemaVersion = 1,
                    Id = petId,
                    Name = name.Trim(),
                    Animations = new Dictionary<string, AnimationDefinition>()
                };

                long totalStagedPngBytes = 0;

                foreach (var (actionName, (fps, sourceFrames)) in actions)
                {
                    var safeAction = actionName.Trim().ToLowerInvariant();
                    if (!PetValidator.AllowedActionNames.Contains(safeAction))
                    {
                        throw new ArgumentException($"Tên hành động không hợp lệ: '{actionName}'. Chỉ chấp nhận 'idle' và 'walk'.", nameof(actions));
                    }

                    if (fps < 1 || fps > 60)
                    {
                        throw new ArgumentException($"FPS của hành động '{actionName}' phải trong khoảng 1–60.");
                    }

                    var actionDir = Path.Combine(stagingDir, safeAction);
                    Directory.CreateDirectory(actionDir);

                    var committedAnim = new AnimationDefinition
                    {
                        Fps = fps,
                        Frames = new List<string>()
                    };

                    for (int i = 0; i < sourceFrames.Count; i++)
                    {
                        var srcPath = sourceFrames[i];
                        if (!File.Exists(srcPath))
                        {
                            throw new FileNotFoundException($"Không tìm thấy file frame: {srcPath}");
                        }

                        var frameName = $"{i + 1:D3}.png";
                        var dstRel = $"{safeAction}/{frameName}";
                        var dstFull = Path.Combine(stagingDir, safeAction, frameName);

                        long remainingBudget = PetValidator.MaxTotalPngSizeBytes - totalStagedPngBytes;
                        long fileMax = Math.Min(PetValidator.MaxPngFileSizeBytes, remainingBudget);

                        long copiedBytes = PetValidator.CopyBoundedStream(srcPath, dstFull, fileMax);
                        totalStagedPngBytes += copiedBytes;

                        committedAnim.Frames.Add(dstRel);
                    }

                    committedDef.Animations[safeAction] = committedAnim;
                }

                // Validate staged assets
                PetValidator.ValidatePackageAssets(stagingDir, committedDef);

                // Write manifest
                var stagingManifest = Path.Combine(stagingDir, "pet.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(committedDef, options);
                File.WriteAllText(stagingManifest, json);

                // If editing existing pet: create backup first
                if (Directory.Exists(targetDir))
                {
                    if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
                    Directory.Move(targetDir, backupDir);
                }

                try
                {
                    Directory.Move(stagingDir, targetDir);
                    // Commit success: remove backup
                    if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
                }
                catch
                {
                    // Rollback from backup if move failed
                    if (Directory.Exists(backupDir) && !Directory.Exists(targetDir))
                    {
                        Directory.Move(backupDir, targetDir);
                    }
                    throw;
                }

                committedDef.RootDirectory = targetDir;
                return committedDef;
            }
            catch
            {
                if (Directory.Exists(stagingDir))
                {
                    try { Directory.Delete(stagingDir, true); } catch { }
                }
                throw;
            }
        }
    }

    public bool SeedDefaultPetIfNeeded()
    {
        var existing = GetAllPets();
        if (existing.Count > 0)
        {
            return false;
        }

        if (!Directory.Exists(_paths.DefaultPetSourceDirectory))
        {
            return false;
        }

        try
        {
            ImportPackage(_paths.DefaultPetSourceDirectory);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
