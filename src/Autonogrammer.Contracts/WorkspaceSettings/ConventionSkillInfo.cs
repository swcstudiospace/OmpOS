using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>A skill the workspace holds.</summary>
public sealed record ConventionSkillInfo
{
    /// <summary>Gets the skill name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description, which may be empty.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the globs that scope the skill; empty when it applies everywhere.</summary>
    public required IReadOnlyList<string> Paths { get; init; }

    /// <summary>Gets a value indicating whether the model may not pick the skill by itself.</summary>
    public required bool DisableModelInvocation { get; init; }

    /// <summary>Gets a value indicating whether the skill is offered after a slash.</summary>
    public required bool UserInvocable { get; init; }

    /// <summary>Gets the folder, relative to the workspace folder, that a nested skill is scoped to; empty for the root and for user skills.</summary>
    public required string ScopeDirectory { get; init; }

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
