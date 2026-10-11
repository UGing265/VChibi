using System.IO;
using System.Text.Json;
using VChibi.Models;
using VChibi.Services;
using Xunit;

namespace VChibi.Tests.Storage;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public SettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vchibi_set_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsCreatedDefault()
    {
        var store = new SettingsStore(_settingsPath);
        var (settings, result, error) = store.Load();

        Assert.Equal(SettingsLoadResult.CreatedDefault, result);
        Assert.NotNull(settings);
        Assert.True(settings.IsPetVisible);
        Assert.Null(error);
    }

    [Fact]
    public void SaveAndLoad_ValidSettings_PreservesValues()
    {
        var store = new SettingsStore(_settingsPath);
        var original = new AppSettings
        {
            SelectedPetId = "pet-alpha",
            ActivePetId = "pet-beta",
            IsPetVisible = false,
            Pets = new Dictionary<string, PetDisplaySettings>
            {
                ["pet-alpha"] = new() { ScalePercent = 150.0, MovementSpeed = 60.0 }
            }
        };

        bool saved = store.Save(original);
        Assert.True(saved);
        Assert.True(File.Exists(_settingsPath));

        var (loaded, result, error) = store.Load();
        Assert.Equal(SettingsLoadResult.Success, result);
        Assert.Null(error);
        Assert.Equal("pet-alpha", loaded.SelectedPetId);
        Assert.Equal("pet-beta", loaded.ActivePetId);
        Assert.False(loaded.IsPetVisible);
        Assert.True(loaded.Pets.ContainsKey("pet-alpha"));
        Assert.Equal(150.0, loaded.Pets["pet-alpha"].ScalePercent);
        Assert.Equal(60.0, loaded.Pets["pet-alpha"].MovementSpeed);
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsCorruptStateWithoutOverwriting()
    {
        File.WriteAllText(_settingsPath, "{ malformed json ::: invalid }");
        var store = new SettingsStore(_settingsPath);

        var (settings, result, error) = store.Load();
        Assert.Equal(SettingsLoadResult.Corrupt, result);
        Assert.NotNull(error);

        // Verify the corrupt file still exists with its original text
        Assert.Equal("{ malformed json ::: invalid }", File.ReadAllText(_settingsPath));
    }

    [Theory]
    [InlineData(10.0, 40.0)]   // Scale too low (< 25)
    [InlineData(500.0, 40.0)]  // Scale too high (> 400)
    [InlineData(100.0, 2.0)]   // Speed too low (< 5)
    [InlineData(100.0, 300.0)] // Speed too high (> 200)
    public void Load_OutOfRangeValues_ReturnsCorruptState(double badScale, double badSpeed)
    {
        var store = new SettingsStore(_settingsPath);
        var badSettings = new AppSettings
        {
            Pets = new Dictionary<string, PetDisplaySettings>
            {
                ["pet-1"] = new() { ScalePercent = badScale, MovementSpeed = badSpeed }
            }
        };

        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(badSettings));

        var (_, result, error) = store.Load();
        Assert.Equal(SettingsLoadResult.Corrupt, result);
        Assert.Contains("ngoài khoảng cho phép", error);
    }

    [Fact]
    public void Load_NullPetEntry_ReturnsCorruptState()
    {
        var store = new SettingsStore(_settingsPath);
        File.WriteAllText(_settingsPath, "{\"schemaVersion\": 1, \"pets\": { \"pet-null\": null }}");

        var (_, result, error) = store.Load();
        Assert.Equal(SettingsLoadResult.Corrupt, result);
        Assert.Contains("null", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_TopLevelNullPetsMap_ReturnsCorruptState()
    {
        var store = new SettingsStore(_settingsPath);
        File.WriteAllText(_settingsPath, "{\"schemaVersion\": 1, \"pets\": null}");

        var (_, result, error) = store.Load();
        Assert.Equal(SettingsLoadResult.Corrupt, result);
        Assert.Contains("null", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorruptedFile_RemainsUntouched_WhenSaveSkippedOrCancelled()
    {
        var originalCorruptContent = "{\"schemaVersion\": 999, corrupt: true}";
        File.WriteAllText(_settingsPath, originalCorruptContent);

        var store = new SettingsStore(_settingsPath);
        var (_, result, _) = store.Load();
        Assert.Equal(SettingsLoadResult.Corrupt, result);

        // User chooses Cancel -> persistence disabled, no save
        store.IsPersistenceEnabled = false;
        store.Save(new AppSettings());

        // Verify file bytes are unchanged
        Assert.Equal(originalCorruptContent, File.ReadAllText(_settingsPath));
    }

    [Fact]
    public void Save_WhenPersistenceDisabled_DoesNotWriteFile()
    {
        var store = new SettingsStore(_settingsPath)
        {
            IsPersistenceEnabled = false
        };

        bool saved = store.Save(new AppSettings());
        Assert.False(saved);
        Assert.False(File.Exists(_settingsPath));
    }
}
