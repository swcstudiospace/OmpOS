namespace OmpOS.Worktrees;

/// <summary>One file of a run's patch.</summary>
/// <param name="Path">The path relative to the repository root, with forward slashes.</param>
/// <param name="Status">What the run did to the file.</param>
/// <param name="Added">The lines added; 0 for a binary file.</param>
/// <param name="Removed">The lines removed; 0 for a binary file.</param>
/// <param name="IsBinary">Whether git treats the file as binary.</param>
public sealed record WorktreePatchFile(string Path, WorktreeFileStatus Status, int Added, int Removed, bool IsBinary);
