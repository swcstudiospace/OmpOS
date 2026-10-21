using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// A single compose service (the sandbox workload).
/// </summary>
public sealed class ComposeService
{
    /// <summary>Gets or sets the container image.</summary>
    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    /// <summary>Gets or sets the command (omitted when empty).</summary>
    [JsonPropertyName("command")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Command { get; set; }

    /// <summary>Gets or sets the working directory.</summary>
    [JsonPropertyName("working_dir")]
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the environment variables (non-secret).</summary>
    [JsonPropertyName("environment")]
    public IReadOnlyDictionary<string, string> Environment { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the network mode.</summary>
    [JsonPropertyName("network_mode")]
    public string NetworkMode { get; set; } = "none";

    /// <summary>Gets or sets whether the root filesystem is read-only.</summary>
    [JsonPropertyName("read_only")]
    public bool ReadOnly { get; set; } = true;

    /// <summary>Gets the security options.</summary>
    [JsonPropertyName("security_opt")]
    public IReadOnlyList<string> SecurityOpt { get; } = ["no-new-privileges:true"];

    /// <summary>Gets the dropped capabilities.</summary>
    [JsonPropertyName("cap_drop")]
    public IReadOnlyList<string> CapDrop { get; } = ["ALL"];

    /// <summary>Gets or sets the pids limit.</summary>
    [JsonPropertyName("pids_limit")]
    public int PidsLimit { get; set; } = 100;

    /// <summary>Gets or sets the memory limit (omitted when null).</summary>
    [JsonPropertyName("mem_limit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MemoryLimit { get; set; }

    /// <summary>Gets or sets the CPU limit (omitted when null).</summary>
    [JsonPropertyName("cpus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cpus { get; set; }

    /// <summary>Gets the resource labels.</summary>
    [JsonPropertyName("labels")]
    public IDictionary<string, string> Labels { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the volume mounts.</summary>
    [JsonPropertyName("volumes")]
    public ICollection<ComposeVolume> Volumes { get; } = [];
}
