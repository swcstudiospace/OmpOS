using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>
/// The one facade a head constructs for hooks: it discovers the hook files of a workspace, owns the consent and quarantine store, starts the
/// hooks of each session, and reports every run. <see cref="HooksService"/> is the implementation; tests use a fake.
/// </summary>
public interface IHooksService
{
    /// <summary>Raised once for every hook record a dispatch produced, in any session.</summary>
    event EventHandler<HookRunRecordedEventArgs>? RunRecorded;

    /// <summary>Raised when the set of hooks that are running changed: a dispatch started or finished.</summary>
    event EventHandler? RunningChanged;

    /// <summary>
    /// Gets the identifiers of the hooks that are probably running right now: the enabled, consented hooks of the events being dispatched, whose
    /// matcher accepts the call. A hook whose matcher the host cannot evaluate in advance (an <c>omp</c> section hook) counts as running for its event.
    /// </summary>
    IReadOnlyCollection<string> RunningHookIds { get; }

    /// <summary>Gets a value indicating whether the global <c>Run hooks</c> switch is on.</summary>
    bool RunHooks { get; }

    /// <summary>Gets a value indicating whether Claude settings files are discovered (discovery is not trust: consent still applies).</summary>
    bool IncludeThirdPartyConfigs { get; }

    /// <summary>Sets the global <c>Run hooks</c> switch. It applies to sessions that start afterwards.</summary>
    /// <param name="enabled">True to run hooks.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    Task SetRunHooksAsync(bool enabled, CancellationToken cancellationToken);

    /// <summary>Sets whether Claude settings files are discovered.</summary>
    /// <param name="enabled">True to discover them.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    Task SetIncludeThirdPartyConfigsAsync(bool enabled, CancellationToken cancellationToken);

    /// <summary>Returns whether the project hooks of a workspace root may be loaded.</summary>
    /// <param name="root">The workspace root.</param>
    /// <returns>True when the user trusted the root.</returns>
    bool IsWorkspaceTrusted(string root);

    /// <summary>Trusts or distrusts a workspace root for loading project hooks. Trusting does not consent to any hook.</summary>
    /// <param name="root">The workspace root.</param>
    /// <param name="trusted">True to trust it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    Task SetWorkspaceTrustedAsync(string root, bool trusted, CancellationToken cancellationToken);

    /// <summary>Reads the hook files of a workspace and returns the hooks with their state. Nothing runs.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The overview.</returns>
    Task<HooksOverview> LoadOverviewAsync(HooksWorkspace workspace, CancellationToken cancellationToken);

    /// <summary>Records the user's consent for the exact identity of a hook, and the version they approved. Nothing is started.</summary>
    /// <param name="hook">The hook as the overview listed it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consent is stored.</returns>
    Task ApproveAsync(EffectiveHook hook, CancellationToken cancellationToken);

    /// <summary>Records consent for each of the hooks, as the <c>Trust all hooks in this folder</c> gesture does, at this moment's identities. Later edits ask again.</summary>
    /// <param name="hooks">The hooks as the overview listed them.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consents are stored.</returns>
    Task ApproveAllAsync(IReadOnlyList<EffectiveHook> hooks, CancellationToken cancellationToken);

    /// <summary>Withdraws the consent for the exact identity of a hook. The record of the approved version stays.</summary>
    /// <param name="hook">The hook as the overview listed it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consent is withdrawn.</returns>
    Task RevokeAsync(EffectiveHook hook, CancellationToken cancellationToken);

    /// <summary>Enables or disables a hook in Autonogrammer's own state. A Cursor or Claude file is never written.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="enabled">True to enable the hook.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is stored.</returns>
    Task SetHookEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken);

    /// <summary>Lifts the quarantine of a hook source and records why.</summary>
    /// <param name="pluginId">The source identifier, <see cref="EffectiveHook.PluginId"/>.</param>
    /// <param name="reason">Why the quarantine is lifted.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when a quarantine was lifted.</returns>
    Task<bool> LiftQuarantineAsync(string pluginId, string reason, CancellationToken cancellationToken);

    /// <summary>Returns the newest run records of every session in this process, oldest first.</summary>
    /// <param name="max">The most records to return.</param>
    /// <returns>The records.</returns>
    IReadOnlyList<HookRunRecord> RecentRuns(int max);

    /// <summary>
    /// Starts the hooks of one conversation: loads the workspace's hook set, starts the loopback bridge and creates the ACP gate. With the global
    /// switch off the session is returned inactive: no gate, no endpoint, nothing runs.
    /// </summary>
    /// <param name="request">The conversation.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The session. The caller disposes it when the conversation ends.</returns>
    Task<IHooksSession> StartSessionAsync(HooksSessionRequest request, CancellationToken cancellationToken);
}
