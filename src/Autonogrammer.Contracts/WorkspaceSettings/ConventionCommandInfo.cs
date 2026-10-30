using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>A custom slash command the workspace holds. It lists only the winner of a name; the files that lost are diagnostics.</summary>
public sealed record ConventionCommandInfo
{
    /// <summary>Gets the command name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description, which may be empty.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the hint for the text typed after the name, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ArgumentHint { get; init; }

    /// <summary>Gets the prompt template.</summary>
    public required string Template { get; init; }

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
