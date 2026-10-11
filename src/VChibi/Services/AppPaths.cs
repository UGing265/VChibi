using System.IO;

namespace VChibi.Services;

public class AppPaths
{
    public string DataRoot { get; }
    public string PetsDirectory { get; }
    public string SettingsPath { get; }
    public string StagingDirectory { get; }
    public string LockFilePath { get; }
    public string DefaultPetSourceDirectory { get; }

    public AppPaths(string? customDataRoot = null, string? customDefaultPetSourceDirectory = null)
    {
        DataRoot = customDataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VChibi"
        );

        PetsDirectory = Path.Combine(DataRoot, "Pets");
        SettingsPath = Path.Combine(DataRoot, "settings.json");
        StagingDirectory = Path.Combine(DataRoot, ".staging");
        LockFilePath = Path.Combine(DataRoot, ".instance.lock");
        DefaultPetSourceDirectory = customDefaultPetSourceDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultPet");
    }

    public void EnsureDirectoriesCreated()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(PetsDirectory);
        Directory.CreateDirectory(StagingDirectory);
    }

    public string GetPetDirectory(string petId)
    {
        return Path.Combine(PetsDirectory, petId);
    }
}
