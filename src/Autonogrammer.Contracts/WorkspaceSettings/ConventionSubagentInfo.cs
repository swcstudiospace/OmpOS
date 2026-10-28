using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>A subagent definition the workspace holds.</summary>
public sealed record ConventionSubagentInfo
{
    /// <summary>Gets the subagent name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description, which may be empty.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the model the subagent asks for, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    /// <summary>Gets a value indicating whether the subagent must not change files.</summary>
    public required bool ReadOnly { get; init; }

    /// <summary>Gets a value indicating whether the subagent runs in the background.</summary>
    public required bool IsBackground { get; init; }

    /// <summary>Gets the subagent's instructions.</summary>
    public required string Instructions { get; init; }

    /// <summary>Gets where the item lives.</summary>
    public required ConventionScopeKind Scope { get; init; }

    /// <summary>Gets the convention directory that holds the file, for example <c>.cursor</c>, or an empty string for a file at the root itself.</summary>
    public required string SourceDirectory { get; init; }

    /// <summary>Gets the position of <see cref="SourceDirectory"/> in the precedence order (<c>.autonogrammer</c> 0, <c>.omp</c> 1, <c>.cursor</c> 2, <c>.claude</c> 3, <c>.codex</c> 4, <c>.agents</c> 5; lower wins), or null for a file at the root itself.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DirectoryRank { get; init; }

    /// <summary>Gets the path to show: relative to the workspace folder, or <c>~/</c> relative for a user file. It is never an absolute server path.</summary>
    public required string Origin { get; init; }
}
