namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>Opens a workspace file in the editor (a tab of <c>EditorTabs</c>). The editor resolves the path through the workspace file system, which refuses a path that leaves the workspace.</summary>
public interface IEditorOpener
{
    /// <summary>Opens a file.</summary>
    /// <param name="workspacePath">The normalised workspace-relative path.</param>
    /// <param name="line">The line to show, from 1, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="false"/> when the file cannot be opened (missing, a folder, too large).</returns>
    Task<bool> OpenAsync(string workspacePath, int? line, CancellationToken cancellationToken);
}
