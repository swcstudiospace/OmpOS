using System.Collections.Concurrent;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Api.Services;

/// <summary>
/// Remembers the engine session of each API session while a run is open, and what that engine last reported. The API starts one engine session
/// per prompt, so a session has a live engine only while a run is active; the last report stays readable afterwards so a client can still draw
/// the model, mode and thinking chips and the slash palette between runs.
/// </summary>
internal sealed class LiveSessionTracker
{
    private readonly ConcurrentDictionary<string, Tracked> _sessions = new(StringComparer.Ordinal);

    /// <summary>Starts following the engine session of an API session. A later call for the same session replaces the earlier engine session and keeps its last report.</summary>
    /// <param name="sessionId">The API session identifier.</param>
    /// <param name="session">The engine session created for the run.</param>
    public void Track(string sessionId, IOmpSession session)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(session);
        var tracked = _sessions.AddOrUpdate(
            sessionId,
            static (_, engine) => new Tracked(engine, null),
            static (_, existing, engine) => new Tracked(engine, existing.Last),
            session);
        Subscribe(sessionId, tracked.Session);
        Capture(sessionId, session);
    }

    /// <summary>Gets the engine session of an API session when it is open.</summary>
    /// <param name="sessionId">The API session identifier.</param>
    /// <returns>The engine session, or null when no run is open or its engine session has ended.</returns>
    public IOmpSession? TryGetLive(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        if (!_sessions.TryGetValue(sessionId, out var tracked))
        {
            return null;
        }

        return tracked.Session.State is HarnessState.Ready or HarnessState.Streaming or HarnessState.Cancelling
            ? tracked.Session
            : null;
    }

    /// <summary>Gets the last report of an API session's engine.</summary>
    /// <param name="sessionId">The API session identifier.</param>
    /// <returns>The last non-empty report, or null when no engine session ever reported one.</returns>
    public ControlSnapshot? LastKnown(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return _sessions.TryGetValue(sessionId, out var tracked) ? tracked.Last : null;
    }

    /// <summary>Reads the engine session's current report into the last-known snapshot. Empty reports never replace a non-empty one.</summary>
    /// <param name="sessionId">The API session identifier.</param>
    /// <param name="session">The engine session.</param>
    public void Capture(string sessionId, IOmpSession session)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = new ControlSnapshot(ConfigOptionMapper.OptionsOf(session), ConfigOptionMapper.CommandsOf(session));
        if (snapshot.Options.Count == 0 && snapshot.Commands.Count == 0)
        {
            return;
        }

        _ = _sessions.AddOrUpdate(
            sessionId,
            static (_, state) => new Tracked(state.Session, state.Snapshot),
            static (_, existing, state) => ReferenceEquals(existing.Session, state.Session)
                ? new Tracked(existing.Session, Merge(existing.Last, state.Snapshot))
                : existing,
            (Session: session, Snapshot: snapshot));
    }

    private static ControlSnapshot Merge(ControlSnapshot? previous, ControlSnapshot current) => previous is null
        ? current
        : new ControlSnapshot(
            current.Options.Count > 0 ? current.Options : previous.Options,
            current.Commands.Count > 0 ? current.Commands : previous.Commands);

    private void Subscribe(string sessionId, IOmpSession session)
    {
        void Refresh(object? sender, EventArgs args) => Capture(sessionId, session);
        if (session is IOmpSessionOptions options)
        {
            options.OptionsChanged += Refresh;
        }

        if (session is IOmpModelControl model)
        {
            model.ModelChanged += Refresh;
        }

        if (session is IOmpSessionCommands commands)
        {
            commands.CommandsChanged += Refresh;
        }
    }

    private sealed record Tracked(IOmpSession Session, ControlSnapshot? Last);
}
