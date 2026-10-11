using System.Text.Json.Serialization;

namespace VChibi.Models;

public class AppSettings
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("selectedPetId")]
    public string? SelectedPetId { get; set; }

    [JsonPropertyName("activePetId")]
    public string? ActivePetId { get; set; }

    [JsonPropertyName("isPetVisible")]
    public bool IsPetVisible { get; set; } = true;

    [JsonPropertyName("pets")]
    public Dictionary<string, PetDisplaySettings> Pets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PetDisplaySettings GetOrCreatePetSettings(string petId)
    {
        if (Pets.TryGetValue(petId, out var settings))
        {
            return settings;
        }

        var newSettings = new PetDisplaySettings();
        Pets[petId] = newSettings;
        return newSettings;
    }
}
