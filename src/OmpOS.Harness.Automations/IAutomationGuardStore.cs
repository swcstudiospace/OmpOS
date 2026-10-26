namespace OmpOS.Harness.Automations;

/// <summary>
/// The state the guards read and write: pauses and the history of admitted runs. A store that throws refuses the run it
/// was consulted for, so a failing store never admits work.
/// </summary>
public interface IAutomationGuardStore
{
    /// <summary>Returns the pause on the automation, or null when it is not paused.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<AutomationPause?> FindPauseAsync(string automationId, CancellationToken cancellationToken);

    /// <summary>Pauses the automation, replacing any earlier pause.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="pause">The pause, naming its guard and how to resume.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    ValueTask PauseAsync(string automationId, AutomationPause pause, CancellationToken cancellationToken);

    /// <summary>Clears the pause on the automation.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    ValueTask ResumeAsync(string automationId, CancellationToken cancellationToken);

    /// <summary>Counts the runs admitted for the automation that started strictly after <paramref name="since"/>.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="since">The exclusive lower bound of the window.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<int> CountRunsSinceAsync(string automationId, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Records that a run was admitted and started at <paramref name="startedAt"/>.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="startedAt">The injected clock's time at admission.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    ValueTask RecordRunAsync(string automationId, DateTimeOffset startedAt, CancellationToken cancellationToken);
}
