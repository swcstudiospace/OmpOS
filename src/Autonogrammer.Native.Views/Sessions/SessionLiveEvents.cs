using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.AgentLoop.Hooks;

namespace Autonogrammer.Native.Views.Sessions;

/// <summary>
/// The relay between the engine side of a session and its runtime. The engine, the hooks and the remote connection change while a session
/// lives (an engine is composed again after the session is closed and reopened), and the runtime outlives each of them, so the head raises their
/// signals here and the runtime republishes them as its own events. The relay holds no state and keeps no subscriber alive after the runtime is disposed.
/// </summary>
public sealed class SessionLiveEvents
{
    /// <summary>Occurs when a hook did something the thread must show. It may be raised on any thread.</summary>
    public event EventHandler<HookNoticeRaisedEventArgs>? HookNoticeRaised;

    /// <summary>Occurs when the connection of a remote session changed. It may be raised on any thread.</summary>
    public event EventHandler<SessionConnectionChangedEventArgs>? ConnectionChanged;

    /// <summary>Occurs when a turn ended because the connection dropped. It may be raised on any thread.</summary>
    public event EventHandler<TurnInterruptedEventArgs>? TurnInterrupted;

    /// <summary>Raises <see cref="HookNoticeRaised"/>.</summary>
    /// <param name="notice">The notice.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notice"/> is null.</exception>
    public void NotifyHookNotice(HookNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        HookNoticeRaised?.Invoke(this, new HookNoticeRaisedEventArgs(notice));
    }

    /// <summary>Raises <see cref="ConnectionChanged"/>.</summary>
    /// <param name="state">The new connection state.</param>
    /// <param name="attempt">The reconnect attempt the state belongs to, or zero.</param>
    /// <param name="lastFailure">The plain reason of the last failed attempt, or null.</param>
    public void NotifyConnectionChanged(RemoteConnectionState state, int attempt, string? lastFailure) =>
        ConnectionChanged?.Invoke(this, new SessionConnectionChangedEventArgs(state, attempt, lastFailure));

    /// <summary>Raises <see cref="TurnInterrupted"/>.</summary>
    /// <param name="nativeSessionId">The identifier the engine gave the session, or null.</param>
    public void NotifyTurnInterrupted(string? nativeSessionId) =>
        TurnInterrupted?.Invoke(this, new TurnInterruptedEventArgs(nativeSessionId));
}
