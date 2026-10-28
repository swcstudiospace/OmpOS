using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.AgentWorker.Hooks;

/// <summary>
/// Reduces the hooks found for a workspace to the ones a cloud worker may run: project hooks only (the user's files and the machine-wide managed
/// file do not apply in the cloud, as in Cursor), command hooks only (a prompt hook needs a model the worker does not give it), omp-section hooks
/// never, the fourteen events of <see cref="CloudHookEvents"/> only, and hooks that did not opt out of remote runs (<c>runsIn</c> without <c>remote</c>).
/// </summary>
internal static class CloudHookFilter
{
    /// <summary>Tells whether a scope lives in the repository checkout.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns><see langword="true"/> for the project scopes.</returns>
    public static bool IsProjectScope(HookScope scope) => scope is HookScope.Project or HookScope.ClaudeProject or HookScope.ClaudeLocal;

    /// <summary>Applies the filter.</summary>
    /// <param name="all">Every hook the loader found.</param>
    /// <returns>The hooks a cloud worker runs, with the sources and diagnostics of the project files.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="all"/> is null.</exception>
    public static EffectiveHookSet Apply(EffectiveHookSet all)
    {
        ArgumentNullException.ThrowIfNull(all);
        var sources = all.Sources.Where(static source => IsProjectScope(source.Scope)).ToArray();
        var files = sources.Select(static source => source.Path).ToHashSet(StringComparer.Ordinal);
        var hooks = all.Hooks.Where(static hook => Runs(hook)).ToArray();
        return new EffectiveHookSet(hooks, sources, [.. all.Diagnostics.Where(diagnostic => files.Contains(diagnostic.File))]);
    }

    private static bool Runs(EffectiveHook hook) =>
        IsProjectScope(hook.Scope)
        && hook.Type == HookType.Command
        && !hook.IsOmpEvent
        && hook.Event is { } hookEvent
        && CloudHookEvents.IsSupported(hookEvent)
        && hook.RunsIn.HasFlag(HookLocations.Remote);
}
