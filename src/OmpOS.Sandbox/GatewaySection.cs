using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// Gateway section.
/// </summary>
public sealed class GatewaySection
{
    /// <summary>Gets or sets the compute driver.</summary>
    [JsonPropertyName("compute_driver")]
    public string ComputeDriver { get; set; } = "podman";

    /// <summary>Gets or sets the gRPC endpoint.</summary>
    [JsonPropertyName("grpc_endpoint")]
    public string GrpcEndpoint { get; set; } = "unix:///var/run/openshell.sock";
}
