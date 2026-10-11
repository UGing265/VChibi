using System.Text.Json.Serialization;

namespace VChibi.Models;

public class PetDefinition
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("animations")]
    public Dictionary<string, AnimationDefinition> Animations { get; set; } = new();

    [JsonIgnore]
    public string RootDirectory { get; set; } = string.Empty;
}
