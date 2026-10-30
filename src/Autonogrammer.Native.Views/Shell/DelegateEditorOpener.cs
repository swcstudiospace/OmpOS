using Autonogrammer.Presentation.Commands.Handlers;

namespace Autonogrammer.Native.Views.Shell;

/// <summary>Opens a workspace file in the editor through a callback of the shell.</summary>
/// <param name="open">Opens the file (a workspace-relative path) at a line, or at the top when the line is null, and says whether it opened.</param>
internal sealed class DelegateEditorOpener(Func<string, int?, CancellationToken, Task<bool>> open) : IEditorOpener
{
    /// <inheritdoc/>
    public Task<bool> OpenAsync(string workspacePath, int? line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspacePath);
        return open(workspacePath, line, cancellationToken);
    }
}
