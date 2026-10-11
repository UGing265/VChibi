using System.Text.Json.Serialization;

namespace VChibi.Models;

public class AnimationDefinition
{
    [JsonPropertyName("fps")]
    public int Fps { get; set; } = 8;

    [JsonPropertyName("frames")]
    public List<string> Frames { get; set; } = new();
}
