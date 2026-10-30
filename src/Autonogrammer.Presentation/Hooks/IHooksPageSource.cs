namespace Autonogrammer.Presentation.Hooks;

/// <summary>
/// What the Hooks page reads and changes. The presentation layer may not reference the harness, so a head adapts the hooks service to this
/// interface. Expected failures are reported as <see cref="Settings.SettingsSourceException"/> and become the page's error or its inline message.
/// </summary>
public interface IHooksPageSource
{
    /// <summary>Raised after a hook finished or was skipped. It may be raised on any thread.</summary>
    event EventHandler<HookRunItemEventArgs>? RunRecorded;

    /// <summary>Raised when the set of running hooks changed. It may be raised on any thread.</summary>
    event EventHandler? RunningChanged;

    /// <summary>Gets the identifiers of the hooks that are running now.</summary>
    IReadOnlyCollection<string> RunningHookIds { get; }

    /// <summary>Reads the hook files of the workspace and the state of every hook. Nothing runs.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The snapshot.</returns>
    Task<HooksSnapshot> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Returns the newest run records, oldest first.</summary>
    /// <param name="max">The most records to return.</param>
    /// <returns>The records.</returns>
    IReadOnlyList<HookRunItem> RecentRuns(int max);

    /// <summary>Switches the global Run hooks setting.</summary>
    /// <param name="enabled">True to run hooks.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the setting is stored.</returns>
    Task SetRunHooksAsync(bool enabled, CancellationToken cancellationToken);

    /// <summary>Trusts the workspace so its project hook files are read. It does not consent to any hook.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the workspace is trusted.</returns>
    Task TrustWorkspaceAsync(CancellationToken cancellationToken);

    /// <summary>Switches a hook on or off in Autonogrammer's own state. A Cursor or Claude file is never written.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="enabled">True to switch the hook on.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is stored.</returns>
    Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken);

    /// <summary>Records consent for the exact hook as it is now. Nothing is started.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consent is stored.</returns>
    Task ApproveAsync(string hookId, CancellationToken cancellationToken);

    /// <summary>Records consent for every listed hook that has none, as they are now: the Trust all hooks in this folder gesture. Later edits ask again.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consents are stored.</returns>
    Task ApproveAllAsync(CancellationToken cancellationToken);

    /// <summary>Withdraws the consent for the exact hook.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consent is withdrawn.</returns>
    Task RevokeAsync(string hookId, CancellationToken cancellationToken);

    /// <summary>Lifts the quarantine of a hook source and records why.</summary>
    /// <param name="source">The quarantined source, <see cref="HookItem.QuarantinedSource"/>.</param>
    /// <param name="reason">Why the quarantine is lifted.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the quarantine is lifted.</returns>
    Task LiftQuarantineAsync(string source, string reason, CancellationToken cancellationToken);
}
