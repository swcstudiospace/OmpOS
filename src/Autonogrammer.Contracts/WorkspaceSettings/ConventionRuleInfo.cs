using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>A rule or an instruction file the workspace holds.</summary>
public sealed record ConventionRuleInfo
{
    /// <summary>Gets the rule name: its path under the <c>rules</c> folder without the extension, or the path of an instruction file.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the form of the file.</summary>
    public required ConventionRuleType Type { get; init; }

    /// <summary>Gets the description, which may be empty.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the globs the rule is tied to; empty when it is not tied to files.</summary>
    public required IReadOnlyList<string> Globs { get; init; }

    /// <summary>Gets a value indicating whether the rule always applies.</summary>
    public required bool AlwaysApply { get; init; }

    /// <summary>Gets the folder, relative to the workspace folder, that a nested rule applies to; empty for the root and for user rules.</summary>
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
