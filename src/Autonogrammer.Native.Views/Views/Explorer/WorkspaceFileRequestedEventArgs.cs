using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Native.Views.Explorer;

/// <summary>Carries the file the user asked to open from the explorer. The explorer runs nothing; the orchestrator decides what opening means.</summary>
/// <param name="entry">The file entry the user activated.</param>
internal sealed class WorkspaceFileRequestedEventArgs(WorkspaceEntry entry) : EventArgs
{
    /// <summary>Gets the file entry the user activated.</summary>
    public WorkspaceEntry Entry { get; } = entry ?? throw new ArgumentNullException(nameof(entry));
}
