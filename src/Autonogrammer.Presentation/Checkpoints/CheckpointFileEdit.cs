namespace Autonogrammer.Presentation.Checkpoints;

/// <summary>The net edit of one file across one turn.</summary>
/// <param name="Path">The path relative to the workspace root, with forward slashes.</param>
/// <param name="OldText">The text before the turn's first edit, or <see langword="null"/> when the turn created the file.</param>
/// <param name="NewText">The text after the turn's last edit.</param>
public sealed record CheckpointFileEdit(string Path, string? OldText, string NewText);
