using System.Collections.Frozen;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Every hook of every source, in merge order, plus the sources and the problems found while loading. The set is
/// immutable; a reload builds a new one and the dispatcher swaps it in atomically.
/// </summary>
public sealed class EffectiveHookSet
{
    private readonly FrozenDictionary<HookEvent, IReadOnlyList<EffectiveHook>> _byEvent;
    private readonly FrozenDictionary<string, IReadOnlyList<EffectiveHook>> _byOmpEvent;

    /// <summary>
    /// Creates a set. <paramref name="hooks"/> is in merge order and is not reordered.
    /// </summary>
    /// <param name="hooks">Every hook, in merge order.</param>
    /// <param name="sources">The files the hooks were read from, in merge order.</param>
    /// <param name="diagnostics">The problems found while loading.</param>
    public EffectiveHookSet(
        IReadOnlyList<EffectiveHook> hooks,
        IReadOnlyList<HookSource> sources,
        IReadOnlyList<HookDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(diagnostics);
        Hooks = hooks;
        Sources = sources;
        Diagnostics = diagnostics;
        _byEvent = hooks
            .Where(hook => hook is { IsOmpEvent: false, Event: not null })
            .GroupBy(hook => hook.Event!.Value)
            .ToFrozenDictionary(group => group.Key, group => (IReadOnlyList<EffectiveHook>)[.. group]);
        _byOmpEvent = hooks
            .Where(hook => hook.IsOmpEvent)
            .GroupBy(hook => hook.EventName, StringComparer.Ordinal)
            .ToFrozenDictionary(group => group.Key, group => (IReadOnlyList<EffectiveHook>)[.. group], StringComparer.Ordinal);
    }

    /// <summary>The empty set.</summary>
    public static EffectiveHookSet Empty { get; } = new([], [], []);

    /// <summary>Every hook, in merge order, including disabled hooks and hooks of unknown events.</summary>
    public IReadOnlyList<EffectiveHook> Hooks { get; }

    /// <summary>The files the hooks were read from, in merge order.</summary>
    public IReadOnlyList<HookSource> Sources { get; }

    /// <summary>The problems found while loading.</summary>
    public IReadOnlyList<HookDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Returns the hooks of one Cursor event, in merge order.
    /// </summary>
    /// <param name="hookEvent">The event.</param>
    /// <returns>The hooks; empty when none.</returns>
    public IReadOnlyList<EffectiveHook> For(HookEvent hookEvent) =>
        _byEvent.TryGetValue(hookEvent, out var hooks) ? hooks : [];

    /// <summary>
    /// Returns the <c>omp</c> section hooks of one omp extension event, in merge order.
    /// </summary>
    /// <param name="ompEvent">The omp event name, for example <c>tool_call</c>.</param>
    /// <returns>The hooks; empty when none.</returns>
    public IReadOnlyList<EffectiveHook> ForOmp(string ompEvent)
    {
        ArgumentNullException.ThrowIfNull(ompEvent);
        return _byOmpEvent.TryGetValue(ompEvent, out var hooks) ? hooks : [];
    }

    /// <summary>
    /// The Cursor event names that have at least one enabled hook. The bridge extension skips the loopback call for others.
    /// </summary>
    public IReadOnlySet<string> ActiveEvents() =>
        _byEvent
            .Where(pair => pair.Value.Any(hook => hook.Enabled))
            .Select(pair => HookEvents.Format(pair.Key))
            .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The omp event names that have at least one enabled <c>omp</c> section hook.
    /// </summary>
    public IReadOnlySet<string> ActiveOmpEvents() =>
        _byOmpEvent
            .Where(pair => pair.Value.Any(hook => hook.Enabled))
            .Select(pair => pair.Key)
            .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The event names for which an unreachable hook host must deny: events with an enabled <c>failClosed</c> hook, and
    /// events where a managed hook guards a permission event (design 3.5).
    /// </summary>
    public IReadOnlySet<string> FailClosedEvents() =>
        Hooks
            .Where(hook => hook.Enabled && !hook.IsOmpEvent && hook.Event is not null
                && (hook.FailClosed || (hook.Scope == HookScope.Managed && hook.IsPermissionHook)))
            .Select(hook => HookEvents.Format(hook.Event!.Value))
            .ToFrozenSet(StringComparer.Ordinal);
}
