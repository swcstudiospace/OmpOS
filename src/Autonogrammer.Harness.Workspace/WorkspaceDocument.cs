namespace Autonogrammer.Workspace;

/// <summary>
/// A rule or skill loaded from a markdown file. The id is the file name without its extension.
/// </summary>
/// <param name="Id">The rule or skill id.</param>
/// <param name="RelativePath">The file path relative to the workspace root, with '/' separators.</param>
/// <param name="Text">The file content as UTF-8 text.</param>
public sealed record WorkspaceDocument(string Id, string RelativePath, string Text);
