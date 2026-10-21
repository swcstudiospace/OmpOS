using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// OpenShell policy YAML model (version 1).
/// </summary>
public sealed class OpenShellPolicy
{
    /// <summary>Gets or sets the policy version.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the filesystem policy.</summary>
    [JsonPropertyName("filesystem_policy")]
    public FilesystemPolicy FilesystemPolicy { get; set; } = new();

    /// <summary>Gets or sets the landlock configuration.</summary>
    [JsonPropertyName("landlock")]
    public LandlockConfig Landlock { get; set; } = new();

    /// <summary>Gets or sets the network policies (default deny when empty).</summary>
    [JsonPropertyName("network_policies")]
    public IReadOnlyList<NetworkPolicyRule> NetworkPolicies { get; set; } = [];
}
