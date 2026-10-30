using System.Collections.Frozen;

namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// Explicit lifecycle state machine for a harness session. The legal moves live in
/// <see cref="AllowedTransitions"/>; <see cref="TransitionTo"/> rejects every other move.
/// Not thread-safe: callers serialize access.
/// </summary>
public sealed class HarnessStateMachine
{
    private static readonly FrozenDictionary<HarnessState, IReadOnlySet<HarnessState>> Table = BuildTable();

    /// <summary>Gets the table of legal transitions, keyed by source state.</summary>
    public static IReadOnlyDictionary<HarnessState, IReadOnlySet<HarnessState>> AllowedTransitions => Table;

    /// <summary>Gets the current state. A new machine starts in <see cref="HarnessState.Disconnected"/>.</summary>
    public HarnessState State { get; private set; } = HarnessState.Disconnected;

    /// <summary>Returns whether the move <paramref name="from"/> → <paramref name="to"/> is in the table.</summary>
    /// <param name="from">The source state.</param>
    /// <param name="to">The target state.</param>
    /// <returns><see langword="true"/> when the move is allowed.</returns>
    public static bool IsAllowed(HarnessState from, HarnessState to) =>
        Table.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Moves the machine to <paramref name="next"/>.</summary>
    /// <param name="next">The target state.</param>
    /// <exception cref="InvalidHarnessTransitionException">The move is not in <see cref="AllowedTransitions"/>.</exception>
    public void TransitionTo(HarnessState next)
    {
        if (!IsAllowed(State, next))
        {
            throw new InvalidHarnessTransitionException(State, next);
        }

        State = next;
    }

    private static FrozenDictionary<HarnessState, IReadOnlySet<HarnessState>> BuildTable() =>
        new Dictionary<HarnessState, IReadOnlySet<HarnessState>>
        {
            [HarnessState.Disconnected] = Targets(HarnessState.Connecting),
            [HarnessState.Connecting] = Targets(HarnessState.Handshaking, HarnessState.Disconnected, HarnessState.Faulted),
            [HarnessState.Handshaking] = Targets(HarnessState.Ready, HarnessState.Disconnected, HarnessState.Faulted),
            [HarnessState.Ready] = Targets(HarnessState.Streaming, HarnessState.Connecting, HarnessState.Disposed, HarnessState.Faulted),
            [HarnessState.Streaming] = Targets(HarnessState.Cancelling, HarnessState.Ready, HarnessState.Faulted),
            [HarnessState.Cancelling] = Targets(HarnessState.Ready, HarnessState.Faulted, HarnessState.Disconnected),
            [HarnessState.Faulted] = Targets(HarnessState.Connecting, HarnessState.Disposed),
            [HarnessState.Disposed] = Targets(),
        }.ToFrozenDictionary();

    private static FrozenSet<HarnessState> Targets(params HarnessState[] targets) => targets.ToFrozenSet();
}
