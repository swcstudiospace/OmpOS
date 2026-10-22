namespace OmpOS.Harness.Plugins;

/// <summary>
/// The result of running the hooks for one lifecycle event.
/// </summary>
/// <param name="Trigger">The lifecycle event.</param>
/// <param name="DenialReason">Why a blocking hook denied the action, or null when the action was not denied.</param>
/// <param name="Executions">One record for each enabled hook for the event, in run order.</param>
public sealed record HookRunResult(HookTrigger Trigger, HookDenialReason? DenialReason, IReadOnlyList<HookExecution> Executions)
{
    /// <summary>True when a blocking hook denied the action, for either reason.</summary>
    public bool ActionDenied => DenialReason is not null;
}
