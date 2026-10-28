namespace Autonogrammer.Presentation.Commands;

/// <summary>A skill that discovery found as <c>skills/&lt;name&gt;/SKILL.md</c>.</summary>
/// <param name="Name">The skill name, which is the name typed after the slash.</param>
/// <param name="Description">What the skill is for; empty when the file gives none.</param>
/// <param name="UserInvocable"><see langword="false"/> hides the skill from the palette and from a typed slash.</param>
/// <param name="Path">The absolute path of the <c>SKILL.md</c> in the file source that read it. It is handed to the engine so the engine reads the skill itself.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="Origin">The file to show: relative to the workspace for a project file, <c>~/</c> relative for a user file.</param>
public sealed record SlashDiscoveredSkill(string Name, string Description, bool UserInvocable, string Path, SlashScope Scope, string Origin);
