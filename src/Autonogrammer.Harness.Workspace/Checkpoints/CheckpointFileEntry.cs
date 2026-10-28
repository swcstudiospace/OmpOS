namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>One file a checkpointed turn changed.</summary>
/// <param name="Root">The absolute real path of the workspace folder that holds the file.</param>
/// <param name="Path">The path relative to <paramref name="Root"/>, with forward slashes.</param>
/// <param name="BeforeHash">The hash of the content before the turn, or <see langword="null"/> when the turn created the file. Line endings and a byte order mark do not change the hash of a text file.</param>
/// <param name="AfterHash">The hash of the content after the turn, or <see langword="null"/> when the turn deleted the file.</param>
/// <param name="BeforeSize">The size in bytes of the content before the turn.</param>
/// <param name="AfterSize">The size in bytes of the content after the turn.</param>
/// <param name="Restorable"><see langword="true"/> when a rewind can bring the file back: its earlier content is kept, or the turn created it and a rewind deletes it.</param>
/// <param name="Note">Why the file cannot be restored (binary, too large), or an empty string.</param>
public sealed record CheckpointFileEntry(
    string Root,
    string Path,
    string? BeforeHash,
    string? AfterHash,
    long BeforeSize,
    long AfterSize,
    bool Restorable,
    string Note);
