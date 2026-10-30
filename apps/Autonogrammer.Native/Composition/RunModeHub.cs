using Autonogrammer.Harness.AgentLoop.Safety;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// The head's single place for the run mode: the store the mode is saved in, the audit log every decision is recorded in and the policy holders of the open sessions. Saving the mode on the
/// Run mode page refreshes every holder, so the next tool call of every session is decided under the new mode; omp itself is told its approval mode when it starts, so a session whose
/// omp is already running keeps the approval mode it started with until its engine is composed again.
/// </summary>
internal sealed class RunModeHub
{
    private readonly Lock _gate = new();
    private readonly List<RunModePolicyHolder> _holders = [];
    private readonly Func<string, bool> _isTrusted;

    /// <summary>Creates the hub.</summary>
    /// <param name="homeDirectory">The home directory the user permission and sandbox files live under and <c>~</c> expands to.</param>
    /// <param name="store">The store the run mode is read from and saved in.</param>
    /// <param name="isTrusted">Returns whether the project files of a workspace root may widen what is allowed (the workspace trust the hooks use).</param>
    /// <param name="clock">The clock behind the audit log, or null for the system clock.</param>
    /// <exception cref="ArgumentException"><paramref name="homeDirectory"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="isTrusted"/> is null.</exception>
    public RunModeHub(string homeDirectory, IRunModeStore store, Func<string, bool> isTrusted, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(isTrusted);
        HomeDirectory = homeDirectory;
        Store = store;
        Clock = clock ?? TimeProvider.System;
        _isTrusted = isTrusted;
    }

    /// <summary>Gets the home directory the user files live under.</summary>
    public string HomeDirectory { get; }

    /// <summary>Gets the store the run mode is read from and saved in.</summary>
    public IRunModeStore Store { get; }

    /// <summary>Gets the clock behind the audit log.</summary>
    public TimeProvider Clock { get; }

    /// <summary>Gets the log of the decisions the run mode made: which rule matched which call.</summary>
    public RunModeAuditLog Audit { get; } = new();

    /// <summary>Returns whether the project files of a workspace root are trusted.</summary>
    /// <param name="workspaceRoot">The workspace root.</param>
    /// <returns>True when the root is trusted.</returns>
    public bool IsTrusted(string workspaceRoot) => _isTrusted(workspaceRoot);

    /// <summary>Reads the run mode the user saved. A missing or invalid file is the default mode.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The mode.</returns>
    public Task<RunMode> LoadModeAsync(CancellationToken cancellationToken) => Store.LoadAsync(cancellationToken);

    /// <summary>Creates the policy holder of a workspace root. The holder is refreshed with every other holder until it is disposed.</summary>
    /// <param name="workspaceRoot">The absolute workspace root.</param>
    /// <returns>The holder. It holds the default policy until it is refreshed.</returns>
    public RunModePolicyHolder Open(string workspaceRoot)
    {
        var holder = new RunModePolicyHolder(this, workspaceRoot);
        lock (_gate)
        {
            _holders.Add(holder);
        }

        return holder;
    }

    /// <summary>Reads the run mode and the files again for every open holder.</summary>
    /// <param name="cancellationToken">Cancels the refresh.</param>
    /// <returns>A task that completes when every holder is current.</returns>
    public async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        RunModePolicyHolder[] holders;
        lock (_gate)
        {
            holders = [.. _holders];
        }

        foreach (var holder in holders)
        {
            await holder.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Stops refreshing a holder. The holder calls it when it is disposed.</summary>
    /// <param name="holder">The holder.</param>
    internal void Remove(RunModePolicyHolder holder)
    {
        lock (_gate)
        {
            _ = _holders.Remove(holder);
        }
    }
}
