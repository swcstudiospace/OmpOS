namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Keeps user-defined automations, their run history and their schedule cursors. The store holds no secret value: the webhook
/// signing secret is kept in an <see cref="IAutomationSecretStore"/>. Owner checks belong to the caller; the store holds every owner's
/// automations.
/// </summary>
public interface IAutomationStore
{
    /// <summary>Lists every automation, in no promised order.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A snapshot of the definitions.</returns>
    ValueTask<IReadOnlyList<UserAutomation>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Reads one automation.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The definition, or null when none has the identifier.</returns>
    ValueTask<UserAutomation?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces an automation. A replacement keeps the history and the cursor.</summary>
    /// <param name="automation">The definition.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the definition is stored.</returns>
    ValueTask SaveAsync(UserAutomation automation, CancellationToken cancellationToken);

    /// <summary>Deletes an automation with its history and cursor.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when an automation was deleted.</returns>
    ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>Reads the instant up to which the scheduler has handled the automation's cron occurrences.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The cursor, or null when none was set or the automation does not exist.</returns>
    ValueTask<DateTimeOffset?> GetScheduleCursorAsync(string id, CancellationToken cancellationToken);

    /// <summary>Sets the schedule cursor. Nothing happens for an automation that does not exist.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cursor">The instant up to which occurrences are handled.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the cursor is stored.</returns>
    ValueTask SetScheduleCursorAsync(string id, DateTimeOffset cursor, CancellationToken cancellationToken);

    /// <summary>Reads an automation's run history.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The entries, newest first; empty for an unknown automation.</returns>
    ValueTask<IReadOnlyList<AutomationRunEntry>> GetHistoryAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a run entry, or replaces the entry with the same run identifier. The history keeps the newest
    /// <see cref="AutomationLimits.MaxHistoryEntries"/> entries. Nothing happens for an automation that does not exist.
    /// </summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the entry is stored.</returns>
    ValueTask UpsertRunAsync(string id, AutomationRunEntry entry, CancellationToken cancellationToken);
}
