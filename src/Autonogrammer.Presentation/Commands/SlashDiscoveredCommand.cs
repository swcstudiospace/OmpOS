namespace Autonogrammer.Presentation.Commands;

/// <summary>A custom slash command that discovery found in a <c>commands</c> folder (<c>.autonogrammer</c>, <c>.omp</c>, <c>.cursor</c>, <c>.claude</c>, <c>.codex</c> or <c>.agents</c>).</summary>
/// <param name="Name">The name typed after the slash.</param>
/// <param name="Description">What the command does; empty when the file gives none.</param>
/// <param name="ArgumentHint">The hint for the text typed after the name, or <see langword="null"/>.</param>
/// <param name="Template">The prompt template. <c>$ARGUMENTS</c>, <c>$@</c>, <c>$1</c> and <c>$@[2:3]</c> are replaced by the typed arguments.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="Origin">The file to show: relative to the workspace for a project file, <c>~/</c> relative for a user file.</param>
public sealed record SlashDiscoveredCommand(string Name, string Description, string? ArgumentHint, string Template, SlashScope Scope, string Origin);
