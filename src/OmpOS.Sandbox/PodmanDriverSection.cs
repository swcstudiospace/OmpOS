using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// Podman driver section.
/// </summary>
public sealed class PodmanDriverSection
{
    /// <summary>Gets or sets the socket path.</summary>
    [JsonPropertyName("socket_path")]
    public string SocketPath { get; set; } = "/run/user/1000/podman/podman.sock";

    /// <summary>Gets or sets the network name.</summary>
    [JsonPropertyName("network_name")]
    public string NetworkName { get; set; } = "openshell";

    /// <summary>Gets or sets the sandbox runtime image.</summary>
    [JsonPropertyName("sandbox_runtime_image")]
    public string SandboxRuntimeImage { get; set; } = "ghcr.io/nvidia/openshell-sandbox-runtime:latest";

    /// <summary>Gets or sets the supervisor image.</summary>
    [JsonPropertyName("supervisor_image")]
    public string SupervisorImage { get; set; } = "ghcr.io/nvidia/openshell-supervisor:latest";

    /// <summary>Gets or sets the image pull policy.</summary>
    [JsonPropertyName("image_pull_policy")]
    public string ImagePullPolicy { get; set; } = "IfNotPresent";

    /// <summary>Gets or sets the stop timeout in seconds.</summary>
    [JsonPropertyName("stop_timeout_secs")]
    public int StopTimeoutSecs { get; set; } = 30;

    /// <summary>Gets or sets the gRPC endpoint.</summary>
    [JsonPropertyName("grpc_endpoint")]
    public string GrpcEndpoint { get; set; } = "unix:///var/run/openshell.sock";
}
