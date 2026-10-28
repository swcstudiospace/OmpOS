using System.Text.Json.Serialization;

namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>A hooks or MCP configuration file that exists. It is listed and not parsed here; <c>GET /hooks</c> parses the hooks files.</summary>
public sealed record ConventionConfigFileInfo
{
    /// <summary>Gets where the item lives.</summary>
    public required ConventionScopeKind Scope { get; init; }

    /// <summary>Gets the convention directory that holds the file, for example <c>.cursor</c>, or an empty string for a file at the root itself.</summary>
    public required string SourceDirectory { get; init; }

    /// <summary>Gets the position of <see cref="SourceDirectory"/> in the precedence order (<c>.ompos</c> 0, <c>.omp</c> 1, <c>.cursor</c> 2, <c>.claude</c> 3, <c>.codex</c> 4, <c>.agents</c> 5; lower wins), or null for a file at the root itself.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DirectoryRank { get; init; }

    /// <summary>Gets the path to show: relative to the workspace folder, or <c>~/</c> relative for a user file. It is never an absolute server path.</summary>
    public required string Origin { get; init; }
}
