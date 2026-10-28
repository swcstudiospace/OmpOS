namespace OmpOS.Presentation.Mentions;

/// <summary>One file or folder of the workspace tree.</summary>
/// <param name="Path">The path relative to the workspace root, with forward slashes and no leading or trailing slash.</param>
/// <param name="IsDirectory">Whether the entry is a folder.</param>
/// <param name="SizeBytes">The size of a file in bytes; 0 for a folder.</param>
public sealed record MentionFileEntry(string Path, bool IsDirectory, long SizeBytes);
