namespace OmpOS.Presentation.Mentions;

/// <summary>
/// Says which workspace paths ignore files exclude. The wiring adapts <c>IgnoreMatcher</c> of <c>OmpOS.Harness.Workspace.Ignore</c>; Presentation
/// does not reference that library.
/// </summary>
public interface IMentionIgnoreFilter
{
    /// <summary>Gets whether a path is excluded from mentions.</summary>
    /// <param name="relativePath">The workspace-relative path with forward slashes.</param>
    /// <param name="isDirectory">Whether the path is a folder.</param>
    /// <returns><see langword="true"/> when the path must not be offered or attached.</returns>
    bool IsIgnored(string relativePath, bool isDirectory);
}
