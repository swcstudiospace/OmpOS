using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// Typed podman-compose document (JSON is valid YAML for podman-compose).
/// </summary>
public sealed class ComposeDocument
{
    /// <summary>Gets or sets the compose version.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "3.8";

    /// <summary>Gets the services keyed by name.</summary>
    [JsonPropertyName("services")]
    public IDictionary<string, ComposeService> Services { get; } = new Dictionary<string, ComposeService>(StringComparer.Ordinal);

    /// <summary>Gets the networks keyed by name.</summary>
    [JsonPropertyName("networks")]
    public IDictionary<string, ComposeNetwork> Networks { get; } = new Dictionary<string, ComposeNetwork>(StringComparer.Ordinal);
}
