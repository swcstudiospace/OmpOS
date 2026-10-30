using Autonogrammer.Harness.Plugins;
using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>One hook of the effective set with everything the Hooks page shows about it: consent, quarantine, the user's switch, its last run and the version the user last approved.</summary>
public sealed record HookInfo
{
    /// <summary>Gets the hook.</summary>
    public required EffectiveHook Hook { get; init; }

    /// <summary>Gets a value indicating whether the hook may run: the file does not switch it off and the user did not.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Gets a value indicating whether consent is recorded for the hook's exact identity, or the hook is pre-consented.</summary>
    public required bool IsConsented { get; init; }

    /// <summary>Gets the quarantine of the hook's source, or null.</summary>
    public PluginQuarantine? Quarantine { get; init; }

    /// <summary>Gets the most recent record of the hook in this process, or null when it has not been considered.</summary>
    public HookRunRecord? LastRun { get; init; }

    /// <summary>Gets the version the user last approved, or null when they never did.</summary>
    public HookApprovalSnapshot? LastApproved { get; init; }
}
