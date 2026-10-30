using Autonogrammer.Presentation.Changes;
using Autonogrammer.Presentation.Commands.Handlers;

namespace Autonogrammer.Native.Views.Shell;

/// <summary>Shows the Changes bar of one session on request: the transcript comes back (a tool in its place closes) and focus goes to the bar's review button.</summary>
/// <param name="changes">The session's change set.</param>
/// <param name="show">Brings the transcript and the bar into view.</param>
internal sealed class ChangesBarOpener(ChangeSetModel changes, Action show) : IChangesBarOpener
{
    /// <inheritdoc/>
    public bool HasChanges => changes.HasChanges;

    /// <inheritdoc/>
    public void Show() => show();
}
