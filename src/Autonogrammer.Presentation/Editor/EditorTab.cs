namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// One open file in the editor. <see cref="RelativePath"/> is the normalised workspace path that identifies the tab; the
/// <see cref="Document"/> holds the buffer that the tab edits.
/// </summary>
/// <param name="RelativePath">The normalised workspace-relative path of the file.</param>
/// <param name="Document">The buffer for the file.</param>
public sealed record EditorTab(string RelativePath, TextDocument Document)
{
    /// <summary>Gets the file name: the last segment of <see cref="RelativePath"/>.</summary>
    public string Name => RelativePath[(RelativePath.LastIndexOf('/') + 1)..];
}
