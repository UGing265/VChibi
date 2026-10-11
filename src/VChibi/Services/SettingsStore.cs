using System.IO;
using System.Text.Json;
using VChibi.Models;

namespace VChibi.Services;

public enum SettingsLoadResult
{
    Success,
    CreatedDefault,
    Corrupt
}

public class SettingsStore
{
    private readonly string _settingsPath;
    private readonly object _lock = new();

    public bool IsPersistenceEnabled { get; set; } = true;

    public SettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public (AppSettings Settings, SettingsLoadResult Result, string? ErrorMessage) Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_settingsPath))
            {
                var defaults = new AppSettings();
                return (defaults, SettingsLoadResult.CreatedDefault, null);
            }

            try
            {
                var json = File.ReadAllText(_settingsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    IsPersistenceEnabled = false;
                    return (new AppSettings(), SettingsLoadResult.Corrupt, "File settings.json trống.");
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var settings = JsonSerializer.Deserialize<AppSettings>(json, options);
                if (settings == null)
                {
                    IsPersistenceEnabled = false;
                    return (new AppSettings(), SettingsLoadResult.Corrupt, "Không thể giải mã settings.json.");
                }

                if (settings.SchemaVersion != 1)
                {
                    IsPersistenceEnabled = false;
                    return (new AppSettings(), SettingsLoadResult.Corrupt, $"SchemaVersion {settings.SchemaVersion} không hợp lệ.");
                }

                // Validate each pet display setting
                if (settings.Pets == null)
                {
                    IsPersistenceEnabled = false;
                    return (new AppSettings(), SettingsLoadResult.Corrupt, "Dữ liệu cấu hình pets bị null.");
                }

                var validatedPets = new Dictionary<string, PetDisplaySettings>(StringComparer.OrdinalIgnoreCase);
                foreach (var (petId, disp) in settings.Pets)
                {
                    if (string.IsNullOrWhiteSpace(petId) || disp == null)
                    {
                        IsPersistenceEnabled = false;
                        return (new AppSettings(), SettingsLoadResult.Corrupt, "Dữ liệu cấu hình pet có chứa mục null hoặc ID trống.");
                    }

                    if (!PetDisplaySettings.IsValidScale(disp.ScalePercent) || !PetDisplaySettings.IsValidSpeed(disp.MovementSpeed))
                    {
                        IsPersistenceEnabled = false;
                        return (new AppSettings(), SettingsLoadResult.Corrupt, $"Giá trị scale ({disp.ScalePercent}) hoặc tốc độ ({disp.MovementSpeed}) của pet '{petId}' nằm ngoài khoảng cho phép.");
                    }

                    validatedPets[petId] = disp;
                }

                settings.Pets = validatedPets;
                return (settings, SettingsLoadResult.Success, null);
            }
            catch (Exception ex)
            {
                IsPersistenceEnabled = false;
                return (new AppSettings(), SettingsLoadResult.Corrupt, ex.Message);
            }
        }
    }

    public bool Save(AppSettings settings)
    {
        if (!IsPersistenceEnabled)
        {
            return false;
        }

        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var tempPath = _settingsPath + ".tmp";
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(tempPath, json);

                // Atomic replace
                File.Move(tempPath, _settingsPath, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
