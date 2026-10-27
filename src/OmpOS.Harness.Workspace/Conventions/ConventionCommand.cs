namespace OmpOS.Workspace.Conventions;

/// <summary>A custom slash command found in a <c>commands</c> folder.</summary>
/// <param name="Name">The command name: the file name without <c>.md</c>.</param>
/// <param name="Description">The frontmatter description, or the first line of the body when the file has none.</param>
/// <param name="ArgumentHint">The frontmatter <c>argument-hint</c>, or <see langword="null"/> when the file has none.</param>
/// <param name="Template">The body: the prompt template with <c>$ARGUMENTS</c>, <c>$@</c>, <c>$1</c> and <c>$@[2:3]</c> placeholders.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="SourceDirectory">The convention directory that holds the file, for example <c>.cursor</c>.</param>
/// <param name="Path">The file path in the source that read it.</param>
/// <param name="Origin">The path to show: relative to the workspace root, or <c>~/</c> relative for a user file.</param>
public sealed record ConventionCommand(
    string Name,
    string Description,
    string? ArgumentHint,
    string Template,
    ConventionScope Scope,
    string SourceDirectory,
    string Path,
    string Origin);
