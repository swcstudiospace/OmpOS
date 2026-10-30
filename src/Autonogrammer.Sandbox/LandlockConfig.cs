using System.Text.Json.Serialization;

namespace Autonogrammer.Sandbox;

/// <summary>
/// Landlock configuration.
/// </summary>
public sealed class LandlockConfig
{
    /// <summary>Gets or sets the landlock compatibility mode.</summary>
    [JsonPropertyName("compatibility")]
    public string Compatibility { get; set; } = "hard_requirement";
}
