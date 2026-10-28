using OmpOS.Presentation.Commands;

namespace OmpOS.Presentation.Modes;

/// <summary>A skill that can be pinned as a custom mode: a user-invocable <c>SKILL.md</c> that convention discovery found.</summary>
/// <param name="Name">The skill name.</param>
/// <param name="Description">What the skill is for; empty when the file gives none.</param>
/// <param name="Icon">The frontmatter <c>icon</c>, or <see langword="null"/>.</param>
/// <param name="Color">The frontmatter <c>color</c>, or <see langword="null"/>.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="Origin">The file to show: relative to the workspace for a project file, <c>~/</c> relative for a user file.</param>
public sealed record ModeSkill(string Name, string Description, string? Icon, string? Color, SlashScope Scope, string Origin);
