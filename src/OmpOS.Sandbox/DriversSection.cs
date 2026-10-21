using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// Drivers section.
/// </summary>
public sealed class DriversSection
{
    /// <summary>Gets or sets the podman driver configuration.</summary>
    [JsonPropertyName("podman")]
    public PodmanDriverSection Podman { get; set; } = new();
}
