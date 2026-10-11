using System.Text.Json.Serialization;

namespace VChibi.Models;

public class PetDisplaySettings
{
    public const double MinScale = 25.0;
    public const double MaxScale = 400.0;
    public const double DefaultScale = 100.0;

    public const double MinSpeed = 5.0;
    public const double MaxSpeed = 200.0;
    public const double DefaultSpeed = 40.0;

    [JsonPropertyName("scalePercent")]
    public double ScalePercent { get; set; } = DefaultScale;

    [JsonPropertyName("movementSpeed")]
    public double MovementSpeed { get; set; } = DefaultSpeed;

    public static bool IsValidScale(double scale) => !double.IsNaN(scale) && !double.IsInfinity(scale) && scale >= MinScale && scale <= MaxScale;
    public static bool IsValidSpeed(double speed) => !double.IsNaN(speed) && !double.IsInfinity(speed) && speed >= MinSpeed && speed <= MaxSpeed;
}
