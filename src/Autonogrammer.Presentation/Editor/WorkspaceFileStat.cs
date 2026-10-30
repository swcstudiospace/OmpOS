namespace Autonogrammer.Presentation.Editor;

/// <summary>What is known about one path of a workspace without reading it.</summary>
/// <param name="IsDirectory">True for a directory, false for a file.</param>
/// <param name="Length">The size in bytes of a file, or <see langword="null"/> for a directory or when the file system cannot tell.</param>
/// <param name="ModifiedUtc">When the path last changed, or <see langword="null"/> when the file system cannot tell.</param>
public sealed record WorkspaceFileStat(bool IsDirectory, long? Length, DateTimeOffset? ModifiedUtc);
