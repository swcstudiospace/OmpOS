namespace Autonogrammer.Presentation.Editor;

/// <summary>One child of a workspace directory.</summary>
/// <param name="Name">The entry's name: one path segment.</param>
/// <param name="RelativePath">The normalised workspace-relative path of the entry.</param>
/// <param name="IsDirectory">True for a directory, false for a file.</param>
public sealed record WorkspaceEntry(string Name, string RelativePath, bool IsDirectory);
