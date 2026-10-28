using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Catalog;

/// <summary>The answer of <c>GET /workspace</c>: what the top bar of the web head shows.</summary>
public sealed record WorkspaceInfo
{
    /// <summary>Gets the workspace display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the name of the workspace folder (the last path segment only), or null when the API has no workspace folder.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FolderLabel { get; init; }

    /// <summary>Gets the plugin check result.</summary>
    public required PluginCheckResult PluginCheck { get; init; }
}
