namespace OmpOS.Workspace.Conventions;

/// <summary>A skill found as <c>skills/&lt;name&gt;/SKILL.md</c> (or, under <c>.ompos/skills</c> only, as a flat <c>skills/&lt;name&gt;.md</c> file).</summary>
/// <param name="Name">The skill name: the folder name.</param>
/// <param name="Description">The frontmatter description, or the first line of the body when the file has none.</param>
/// <param name="Paths">The frontmatter <c>paths</c> globs that scope the skill; empty when it applies everywhere.</param>
/// <param name="DisableModelInvocation">Whether the model may not pick the skill by itself; the user can still type it.</param>
/// <param name="UserInvocable">Whether the skill is offered after a slash. <see langword="false"/> hides it from the palette.</param>
/// <param name="Icon">The frontmatter <c>icon</c>, or <see langword="null"/>.</param>
/// <param name="Color">The frontmatter <c>color</c>, or <see langword="null"/>.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="ScopeDirectory">The folder, relative to the workspace root, that a nested skill is scoped to; empty for the root and for user skills.</param>
/// <param name="SourceDirectory">The convention directory that holds the skill, for example <c>.claude</c>.</param>
/// <param name="Path">The file path in the source that read it.</param>
/// <param name="Origin">The path to show: relative to the workspace root, or <c>~/</c> relative for a user file.</param>
public sealed record ConventionSkill(
    string Name,
    string Description,
    IReadOnlyList<string> Paths,
    bool DisableModelInvocation,
    bool UserInvocable,
    string? Icon,
    string? Color,
    ConventionScope Scope,
    string ScopeDirectory,
    string SourceDirectory,
    string Path,
    string Origin);
