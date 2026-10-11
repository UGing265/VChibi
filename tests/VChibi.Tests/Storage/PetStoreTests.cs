using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using VChibi.Models;
using VChibi.Services;
using Xunit;

namespace VChibi.Tests.Storage;

public class PetStoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _paths;
    private readonly AppLock _appLock;

    public PetStoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "vchibi_store_tests_" + Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_tempRoot, Path.Combine(_tempRoot, "mock_default_source"));
        _paths.EnsureDirectoriesCreated();
        _appLock = new AppLock(_paths.LockFilePath);
        Assert.True(_appLock.TryAcquire());
    }

    public void Dispose()
    {
        _appLock.Dispose();
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, true); } catch { }
        }
    }

    private string CreateValidPng(string path, int width = 32, int height = 32)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(255, 120, 98, 184));
        bmp.Save(path, ImageFormat.Png);
        return path;
    }

    private string CreateValidSourcePackage(string packageDir, string name = "Sample Idol")
    {
        Directory.CreateDirectory(packageDir);
        var idle01 = CreateValidPng(Path.Combine(packageDir, "idle", "001.png"));
        var idle02 = CreateValidPng(Path.Combine(packageDir, "idle", "002.png"));
        var walk01 = CreateValidPng(Path.Combine(packageDir, "walk", "001.png"));
        var walk02 = CreateValidPng(Path.Combine(packageDir, "walk", "002.png"));

        var manifest = new
        {
            schemaVersion = 1,
            id = "sample-idol",
            name,
            animations = new
            {
                idle = new { fps = 8, frames = new[] { "idle/001.png", "idle/002.png" } },
                walk = new { fps = 8, frames = new[] { "walk/001.png", "walk/002.png" } }
            }
        };

        File.WriteAllText(Path.Combine(packageDir, "pet.json"), JsonSerializer.Serialize(manifest));
        return packageDir;
    }

    [Fact]
    public void Constructor_WithoutAcquiredLock_ThrowsInvalidOperationException()
    {
        var unacquiredLock = new AppLock(Path.Combine(_tempRoot, "unacquired.lock"));
        // Does not call TryAcquire()
        Assert.False(unacquiredLock.IsAcquired);

        var ex = Assert.Throws<InvalidOperationException>(() => new PetStore(_paths, unacquiredLock));
        Assert.Contains("AppLock", ex.Message);
    }

    [Fact]
    public void SecondInstance_FailsToAcquireLock_WhenFirstInstanceActive()
    {
        var secondLock = new AppLock(_paths.LockFilePath);
        bool acquired = secondLock.TryAcquire();
        Assert.False(acquired); // Exclusive lock prevents second instance
        secondLock.Dispose();
    }

    [Fact]
    public void ImportPackage_AssignsNewUniqueId_AndAllowsMultipleImportsWithoutCollision_A11()
    {
        var store = new PetStore(_paths, _appLock);
        var sourceDir = Path.Combine(_tempRoot, "external_source");
        CreateValidSourcePackage(sourceDir, "Idol A");

        var pet1 = store.ImportPackage(sourceDir);
        var pet2 = store.ImportPackage(sourceDir);

        Assert.NotNull(pet1);
        Assert.NotNull(pet2);
        Assert.NotEqual(pet1.Id, pet2.Id);
        Assert.True(Directory.Exists(_paths.GetPetDirectory(pet1.Id)));
        Assert.True(Directory.Exists(_paths.GetPetDirectory(pet2.Id)));

        var allPets = store.GetAllPets();
        Assert.Equal(2, allPets.Count);
    }

    [Fact]
    public void SavePetDraft_CopiesAssets_RemainsValidAfterSourceDeletion_A08()
    {
        var store = new PetStore(_paths, _appLock);
        var stagingSources = Path.Combine(_tempRoot, "temp_sources");
        Directory.CreateDirectory(stagingSources);

        var idleSrc = CreateValidPng(Path.Combine(stagingSources, "src_idle.png"));
        var walkSrc = CreateValidPng(Path.Combine(stagingSources, "src_walk.png"));

        var actions = new Dictionary<string, (int Fps, IReadOnlyList<string> SourceFramePaths)>
        {
            ["idle"] = (8, new[] { idleSrc }),
            ["walk"] = (8, new[] { walkSrc })
        };

        var committedPet = store.SavePetDraft(null, "Created Pet", actions);
        Assert.NotNull(committedPet);

        // Delete source PNG files
        File.Delete(idleSrc);
        File.Delete(walkSrc);
        Assert.False(File.Exists(idleSrc));
        Assert.False(File.Exists(walkSrc));

        // Reload pet from store - must still be completely valid
        var reloaded = store.GetPet(committedPet.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Created Pet", reloaded.Name);
        Assert.Single(reloaded.Animations["idle"].Frames);
        Assert.Single(reloaded.Animations["walk"].Frames);

        var storedIdleFrame = Path.Combine(reloaded.RootDirectory, reloaded.Animations["idle"].Frames[0]);
        Assert.True(File.Exists(storedIdleFrame));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("/root")]
    [InlineData("has space")]
    [InlineData("special*char")]
    public void GetSafePetDirectory_ThrowsOnInvalidOrEscapedId(string badId)
    {
        var store = new PetStore(_paths, _appLock);
        Assert.Throws<ArgumentException>(() => store.GetSafePetDirectory(badId));
    }

    [Fact]
    public void GetAllPackages_ReportsCorruptPackage_A17()
    {
        var store = new PetStore(_paths, _appLock);
        var sourceDir = Path.Combine(_tempRoot, "external_source");
        CreateValidSourcePackage(sourceDir, "Valid Idol");
        store.ImportPackage(sourceDir);

        // Intentionally create a corrupted pet directory
        var corruptPetDir = Path.Combine(_paths.PetsDirectory, "corrupt-pet");
        Directory.CreateDirectory(corruptPetDir);
        File.WriteAllText(Path.Combine(corruptPetDir, "pet.json"), "{ invalid json }");

        var packages = store.GetAllPackages();
        Assert.Equal(2, packages.Count);

        var corruptItem = packages.FirstOrDefault(p => p.IsCorrupt);
        Assert.NotNull(corruptItem);
        Assert.NotNull(corruptItem.ErrorMessage);
        Assert.Contains("Hỏng", corruptItem.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var validPets = store.GetAllPets();
        Assert.Single(validPets);
    }

    [Fact]
    public void RecoverPendingTransactions_WhenInterruptedMidCommit_RestoresOldValidPet()
    {
        var store = new PetStore(_paths, _appLock);
        var sourceDir = Path.Combine(_tempRoot, "external_source");
        CreateValidSourcePackage(sourceDir, "Original Idol");
        var originalPet = store.ImportPackage(sourceDir);
        var originalId = originalPet.Id;

        // Simulate crash mid-commit: target moved to backup, but new draft not yet committed
        var targetDir = _paths.GetPetDirectory(originalId);
        var backupDir = Path.Combine(_paths.StagingDirectory, $"backup-{originalId}");
        Directory.Move(targetDir, backupDir);
        Assert.False(Directory.Exists(targetDir));
        Assert.True(Directory.Exists(backupDir));

        // Reopen store as if application restarted after crash
        var newStoreInstance = new PetStore(_paths, _appLock);

        // Verify old pet was recovered and is fully valid
        Assert.True(Directory.Exists(targetDir));
        Assert.False(Directory.Exists(backupDir));

        var recoveredPet = newStoreInstance.GetPet(originalId);
        Assert.NotNull(recoveredPet);
        Assert.Equal("Original Idol", recoveredPet.Name);
    }

    [Fact]
    public void RecoverPendingTransactions_WhenCommitSucceededBeforeCrash_CleansUpOrphanedBackup()
    {
        var store = new PetStore(_paths, _appLock);
        var sourceDir = Path.Combine(_tempRoot, "external_source");
        CreateValidSourcePackage(sourceDir, "Stable Idol");
        var pet = store.ImportPackage(sourceDir);
        var petId = pet.Id;

        // Simulate leftover backup directory after successful commit
        var backupDir = Path.Combine(_paths.StagingDirectory, $"backup-{petId}");
        Directory.CreateDirectory(backupDir);
        File.WriteAllText(Path.Combine(backupDir, "leftover.txt"), "leftover");

        // Reopen store
        var newStoreInstance = new PetStore(_paths, _appLock);

        // Orphaned backup must be cleaned up because target is already complete and valid
        Assert.False(Directory.Exists(backupDir));
        Assert.NotNull(newStoreInstance.GetPet(petId));
    }

    [Fact]
    public void StorageSmoke_FullLifecycle_SeedCreateDeleteSourceReloadDuplicateImportEditRollback()
    {
        // Step 1: Seed default pet
        var store = new PetStore(_paths, _appLock);
        var fakeDefaultDir = _paths.DefaultPetSourceDirectory;
        CreateValidSourcePackage(fakeDefaultDir, "Default Chibi");
        bool seeded = store.SeedDefaultPetIfNeeded();
        Assert.True(seeded);
        var petsAfterSeed = store.GetAllPets();
        Assert.Single(petsAfterSeed);
        Assert.Equal("Default Chibi", petsAfterSeed[0].Name);

        // Step 2: Create pet from PNG
        var externalSources = Path.Combine(_tempRoot, "smoke_user_downloads");
        Directory.CreateDirectory(externalSources);
        var idlePng = CreateValidPng(Path.Combine(externalSources, "raw_idle.png"));
        var walkPng = CreateValidPng(Path.Combine(externalSources, "raw_walk.png"));

        var createdPet = store.SavePetDraft(null, "Created Smoke Idol", new Dictionary<string, (int Fps, IReadOnlyList<string> SourceFramePaths)>
        {
            ["idle"] = (6, new[] { idlePng }),
            ["walk"] = (8, new[] { walkPng })
        });
        Assert.NotNull(createdPet);
        var createdId = createdPet.Id;

        // Step 3: Delete source PNGs
        File.Delete(idlePng);
        File.Delete(walkPng);
        Assert.False(File.Exists(idlePng));
        Assert.False(File.Exists(walkPng));

        // Step 4: Reload pet from store
        var reloaded = store.GetPet(createdId);
        Assert.NotNull(reloaded);
        Assert.Equal("Created Smoke Idol", reloaded.Name);
        var storedIdlePath = Path.Combine(reloaded.RootDirectory, reloaded.Animations["idle"].Frames[0]);
        Assert.True(File.Exists(storedIdlePath));

        // Step 5: Import same package twice
        var importSource = Path.Combine(_tempRoot, "smoke_import_source");
        CreateValidSourcePackage(importSource, "Package Idol");
        var imp1 = store.ImportPackage(importSource);
        var imp2 = store.ImportPackage(importSource);
        Assert.NotEqual(imp1.Id, imp2.Id);
        Assert.NotNull(store.GetPet(imp1.Id));
        Assert.NotNull(store.GetPet(imp2.Id));

        // Step 6 & 7: Sửa lỗi / hủy -> reload bản cũ
        Assert.Throws<ArgumentException>(() =>
        {
            store.SavePetDraft(createdId, "Broken Edit", new Dictionary<string, (int Fps, IReadOnlyList<string> SourceFramePaths)>
            {
                ["idle"] = (0, new[] { storedIdlePath }), // Invalid FPS
                ["walk"] = (8, new[] { storedIdlePath })
            });
        });

        // Reload original pet - verify completely untouched
        var petAfterFailedEdit = store.GetPet(createdId);
        Assert.NotNull(petAfterFailedEdit);
        Assert.Equal("Created Smoke Idol", petAfterFailedEdit.Name);
        Assert.Equal(6, petAfterFailedEdit.Animations["idle"].Fps);
    }
}
