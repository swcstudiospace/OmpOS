namespace Autonogrammer.Workspace.Conventions;

/// <summary>A rule or an instruction file.</summary>
/// <param name="Name">For a rule file, its path under the <c>rules</c> folder without the extension (for example <c>frontend/style</c>); for an instruction file, its path under the scope (for example <c>AGENTS.md</c> or <c>.claude/CLAUDE.md</c>).</param>
/// <param name="Kind">The form of the file.</param>
/// <param name="Description">The frontmatter description, or the first line of the body when the file has none.</param>
/// <param name="Globs">The frontmatter <c>globs</c>; empty when the rule is not tied to files.</param>
/// <param name="AlwaysApply">The frontmatter <c>alwaysApply</c> flag; <see langword="true"/> for an instruction file, which always applies.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="ScopeDirectory">The folder, relative to the workspace root, that a nested rule applies to; empty for the root and for user rules.</param>
/// <param name="SourceDirectory">The convention directory that holds the rule, or an empty string for a file at the root itself.</param>
/// <param name="Path">The file path in the source that read it.</param>
/// <param name="Origin">The path to show: relative to the workspace root, or <c>~/</c> relative for a user file.</param>
public sealed record ConventionRule(
    string Name,
    ConventionRuleKind Kind,
    string Description,
    IReadOnlyList<string> Globs,
    bool AlwaysApply,
    ConventionScope Scope,
    string ScopeDirectory,
    string SourceDirectory,
    string Path,
    string Origin);
