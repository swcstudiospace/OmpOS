namespace Autonogrammer.Presentation.Automations;

/// <summary>
/// The automations an <see cref="AutomationsViewModel"/> lists and changes, and the load step that makes them ready. The presentation
/// layer may not reference the automation harness (L2 to L3), so a head adapts the automation engine and its guard store to this
/// interface; expected failures are reported as <see cref="AutomationsSourceException"/>.
/// </summary>
public interface IAutomationsSource
{
    /// <summary>Loads the automations. Completes when they are ready, and faults with <see cref="AutomationsSourceException"/> when loading fails.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the automations are ready.</returns>
    Task LoadAsync(CancellationToken cancellationToken);

    /// <summary>Reads every automation, including disabled and paused ones, with its latest run and pause.</summary>
    /// <returns>A snapshot; later changes do not alter it.</returns>
    IReadOnlyList<AutomationEntry> Snapshot();

    /// <summary>Enables or disables an automation. Neither change starts a run.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="enabled">True to enable the automation.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is written.</returns>
    Task SetEnabledAsync(string automationId, bool enabled, CancellationToken cancellationToken);

    /// <summary>Clears the pause on an automation. Resuming neither enables the automation nor starts a run.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the pause is cleared.</returns>
    Task ResumeAsync(string automationId, CancellationToken cancellationToken);
}
