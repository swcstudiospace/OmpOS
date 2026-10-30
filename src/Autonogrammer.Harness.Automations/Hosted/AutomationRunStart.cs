namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// The result of asking the runner to start a run: either the run was admitted and runs in the background, or a guard refused it.
/// </summary>
public sealed class AutomationRunStart
{
    private AutomationRunStart(string? runId, Task<AutomationRunEntry>? completion, AutomationRunRefusal? refusal)
    {
        RunId = runId;
        Completion = completion;
        Refusal = refusal;
    }

    /// <summary>Gets whether the run was admitted.</summary>
    public bool Admitted => Refusal is null;

    /// <summary>Gets the run identifier, or null when the run was refused.</summary>
    public string? RunId { get; }

    /// <summary>Gets the task that completes with the final history entry, or null when the run was refused. It never faults.</summary>
    public Task<AutomationRunEntry>? Completion { get; }

    /// <summary>Gets the guard that refused the run, or null when it was admitted.</summary>
    public AutomationRunRefusal? Refusal { get; }

    /// <summary>Creates the result of an admitted run.</summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="completion">The completion task.</param>
    /// <returns>The result.</returns>
    public static AutomationRunStart Started(string runId, Task<AutomationRunEntry> completion) => new(runId, completion, null);

    /// <summary>Creates the result of a refused run.</summary>
    /// <param name="refusal">Why it was refused.</param>
    /// <returns>The result.</returns>
    public static AutomationRunStart Refused(AutomationRunRefusal refusal) => new(null, null, refusal);
}
