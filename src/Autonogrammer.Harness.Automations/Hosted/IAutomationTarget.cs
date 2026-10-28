namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Where automation runs execute. The runner picks the target whose <see cref="Kind"/> matches the automation. A target enforces the
/// automation's allowed tools and secrets; the runner only carries them.
/// </summary>
public interface IAutomationTarget
{
    /// <summary>Gets the kind of automation this target runs.</summary>
    AutomationTargetKind Kind { get; }

    /// <summary>Starts a run and returns when it has finished, or when it has been handed to something that continues on its own.</summary>
    /// <param name="request">The run.</param>
    /// <param name="cancellationToken">Cancelled when the run exceeds its maximum runtime or the host stops. A target that cannot stop its work returns a result and lets the work continue.</param>
    /// <returns>How the start ended. A target reports expected failures as <see cref="AutomationTargetResult.Failed"/>; an exception is recorded as a failure too.</returns>
    Task<AutomationTargetResult> RunAsync(AutomationRunRequest request, CancellationToken cancellationToken);
}
