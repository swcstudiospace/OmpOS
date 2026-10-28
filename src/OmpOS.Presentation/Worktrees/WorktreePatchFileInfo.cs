namespace OmpOS.Presentation.Worktrees;

/// <summary>One file of a run's patch.</summary>
/// <param name="Path">The path relative to the repository root.</param>
/// <param name="Change">What the run did to it.</param>
/// <param name="Added">The lines added.</param>
/// <param name="Removed">The lines removed.</param>
/// <param name="IsBinary">Whether the file is binary.</param>
public sealed record WorktreePatchFileInfo(string Path, WorktreeFileChange Change, int Added, int Removed, bool IsBinary);
