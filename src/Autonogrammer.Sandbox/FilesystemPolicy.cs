using System.Text.Json.Serialization;

namespace Autonogrammer.Sandbox;

/// <summary>
/// Filesystem policy.
/// </summary>
public sealed class FilesystemPolicy
{
    /// <summary>Gets or sets whether to include the workdir.</summary>
    [JsonPropertyName("include_workdir")]
    public bool IncludeWorkdir { get; set; } = true;

    /// <summary>Gets or sets the read-only paths.</summary>
    [JsonPropertyName("read_only")]
    public IReadOnlyList<string> ReadOnly { get; set; } = [];

    /// <summary>Gets or sets the read-write paths.</summary>
    [JsonPropertyName("read_write")]
    public IReadOnlyList<string> ReadWrite { get; set; } = [];
}
