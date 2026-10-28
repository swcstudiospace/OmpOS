using OmpOS.Presentation.Editor;

namespace OmpOS.Presentation.Remote;

/// <summary>
/// What the next new session starts on: the environment (local or an SSH profile) and, for a workspace set, its folders. The shell sets it from the environment picker or the workspace dialog just before it asks the panel
/// for a new session; the engine set takes it (and clears it) when it creates that session, so a session started while nothing is pending stays local on the default folder. One instance is shared by the shell and the engine set.
/// </summary>
public sealed class PendingSessionTarget
{
    private readonly Lock _gate = new();
    private SessionTarget? _target;
    private IReadOnlyList<WorkspacePickerRoot>? _roots;

    /// <summary>Sets the target the next new session starts on. A local target (or null) clears it.</summary>
    /// <param name="target">The target, or <see langword="null"/> for a local session.</param>
    public void Set(SessionTarget? target)
    {
        lock (_gate)
        {
            _target = target is { Kind: SessionTargetKind.Local } ? null : target;
        }
    }

    /// <summary>Sets the folders of the workspace set the next new session starts on. The first root is the primary folder; the rest are added directories.</summary>
    /// <param name="roots">The folders, or <see langword="null"/> or an empty list for the default folder.</param>
    public void SetRoots(IReadOnlyList<WorkspacePickerRoot>? roots)
    {
        lock (_gate)
        {
            _roots = roots is { Count: > 0 } ? [.. roots] : null;
        }
    }

    /// <summary>Takes the pending target and clears it.</summary>
    /// <returns>The target, or <see langword="null"/> when the next session is local.</returns>
    public SessionTarget? Take()
    {
        lock (_gate)
        {
            var target = _target;
            _target = null;
            return target;
        }
    }

    /// <summary>Takes the pending workspace set folders and clears them.</summary>
    /// <returns>The folders (the primary first), or <see langword="null"/> when the next session uses the default folder.</returns>
    public IReadOnlyList<WorkspacePickerRoot>? TakeRoots()
    {
        lock (_gate)
        {
            var roots = _roots;
            _roots = null;
            return roots;
        }
    }
}
