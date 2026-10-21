using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// OpenShell gateway TOML configuration model (version 2).
/// </summary>
public sealed class OpenShellGatewayConfig
{
    /// <summary>Gets or sets the gateway version.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    /// <summary>Gets or sets the gateway configuration.</summary>
    [JsonPropertyName("gateway")]
    public GatewaySection Gateway { get; set; } = new();

    /// <summary>Gets or sets the drivers configuration.</summary>
    [JsonPropertyName("drivers")]
    public DriversSection Drivers { get; set; } = new();
}
