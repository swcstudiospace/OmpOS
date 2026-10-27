namespace OmpOS.Workspace.Conventions;

/// <summary>A subagent definition found in an <c>agents</c> folder.</summary>
/// <param name="Name">The frontmatter name, or the file name without <c>.md</c> when the file has none.</param>
/// <param name="Description">The frontmatter description, or the first line of the body when the file has none.</param>
/// <param name="Model">The frontmatter <c>model</c> (for example <c>inherit</c> or a model id), or <see langword="null"/>.</param>
/// <param name="ReadOnly">The frontmatter <c>readonly</c> flag.</param>
/// <param name="IsBackground">The frontmatter <c>is_background</c> flag.</param>
/// <param name="Instructions">The body: the subagent's instructions.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="SourceDirectory">The convention directory that holds the file, for example <c>.cursor</c>.</param>
/// <param name="Path">The file path in the source that read it.</param>
/// <param name="Origin">The path to show: relative to the workspace root, or <c>~/</c> relative for a user file.</param>
public sealed record ConventionSubagent(
    string Name,
    string Description,
    string? Model,
    bool ReadOnly,
    bool IsBackground,
    string Instructions,
    ConventionScope Scope,
    string SourceDirectory,
    string Path,
    string Origin);
