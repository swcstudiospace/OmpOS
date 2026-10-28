namespace OmpOS.Workspace.Checkpoints;

/// <summary>One file a rewind may write, with everything the write needs, read when the plan was made.</summary>
/// <param name="File">The public plan entry.</param>
/// <param name="AbsolutePath">The real absolute path of the file.</param>
/// <param name="Content">The earlier content to write, or <see langword="null"/> when the file is deleted or not restorable.</param>
/// <param name="CurrentHash">The hash of the file's content when the plan was made, or <see langword="null"/> when it did not exist.</param>
internal sealed record PlannedWrite(RewindFilePlan File, string AbsolutePath, byte[]? Content, string? CurrentHash);
