using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>The hooks that apply to one workspace, as the Hooks page shows them.</summary>
public sealed record HooksOverview
{
    /// <summary>Gets a value indicating whether the global <c>Run hooks</c> switch is on.</summary>
    public required bool RunHooks { get; init; }

    /// <summary>Gets the workspace roots the overview was loaded for.</summary>
    public required IReadOnlyList<string> WorkspaceRoots { get; init; }

    /// <summary>Gets the effective hook set, without the hooks of withheld sources. It carries the diagnostics of every file.</summary>
    public required EffectiveHookSet Set { get; init; }

    /// <summary>Gets every hook of <see cref="Set"/> with its state, in run order.</summary>
    public required IReadOnlyList<HookInfo> Hooks { get; init; }

    /// <summary>
    /// Gets the project and Claude project files that exist but whose workspace is not trusted. Their hooks are not loaded and not listed
    /// until the user trusts the workspace.
    /// </summary>
    public required IReadOnlyList<HookSource> WithheldSources { get; init; }
}
