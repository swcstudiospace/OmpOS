using OmpOS.Presentation.Hooks;
using OmpOS.Presentation.Settings;

namespace OmpOS.Presentation.Tests.Hooks;

/// <summary>A hooks page source with a scripted snapshot. It records every call and can fail a load or a change.</summary>
internal sealed class ScriptedHooksPageSource : IHooksPageSource
{
    private readonly List<string> _calls = [];
    private readonly List<HookRunItem> _runs = [];

    /// <inheritdoc />
    public event EventHandler<HookRunItemEventArgs>? RunRecorded;

    /// <inheritdoc />
    public event EventHandler? RunningChanged;

    /// <summary>Gets or sets what <see cref="LoadAsync"/> returns.</summary>
    public HooksSnapshot Snapshot { get; set; } = new() { RunHooks = true, Hooks = [] };

    /// <summary>Gets or sets the reason a load fails with, or null.</summary>
    public string? LoadFailure { get; set; }

    /// <summary>Gets or sets the reason a change fails with, or null.</summary>
    public string? ChangeFailure { get; set; }

    /// <summary>Gets the calls received, in order.</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>Gets or sets the identifiers of the running hooks.</summary>
    public IReadOnlyCollection<string> RunningHookIds { get; set; } = [];

    /// <summary>Adds a run the log starts with.</summary>
    /// <param name="run">The run.</param>
    public void AddRun(HookRunItem run) => _runs.Add(run);

    /// <summary>Raises a run as the service does.</summary>
    /// <param name="run">The run.</param>
    public void Raise(HookRunItem run) => RunRecorded?.Invoke(this, new HookRunItemEventArgs(run));

    /// <summary>Raises a change of the running hooks.</summary>
    /// <param name="ids">The identifiers of the running hooks.</param>
    public void RaiseRunning(params string[] ids)
    {
        RunningHookIds = ids;
        RunningChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task<HooksSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        _calls.Add("Load");
        return LoadFailure is { } failure ? Task.FromException<HooksSnapshot>(new SettingsSourceException(failure)) : Task.FromResult(Snapshot);
    }

    /// <inheritdoc />
    public IReadOnlyList<HookRunItem> RecentRuns(int max) => [.. _runs.TakeLast(max)];

    /// <inheritdoc />
    public Task SetRunHooksAsync(bool enabled, CancellationToken cancellationToken)
    {
        _calls.Add($"SetRunHooks({enabled})");
        if (ChangeFailure is null)
        {
            Snapshot = Snapshot with { RunHooks = enabled };
        }

        return ChangeAsync();
    }

    /// <inheritdoc />
    public Task TrustWorkspaceAsync(CancellationToken cancellationToken)
    {
        _calls.Add("TrustWorkspace");
        if (ChangeFailure is null)
        {
            Snapshot = Snapshot with { WithheldFiles = [] };
        }

        return ChangeAsync();
    }

    /// <inheritdoc />
    public Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken)
    {
        _calls.Add($"SetEnabled({hookId}, {enabled})");
        return ChangeHookAsync(hookId, hook => hook with { IsEnabled = enabled });
    }

    /// <inheritdoc />
    public Task ApproveAsync(string hookId, CancellationToken cancellationToken)
    {
        _calls.Add($"Approve({hookId})");
        return ChangeHookAsync(hookId, static hook => hook with { IsConsented = true });
    }

    /// <inheritdoc />
    public Task ApproveAllAsync(CancellationToken cancellationToken)
    {
        _calls.Add("ApproveAll");
        if (ChangeFailure is null)
        {
            Snapshot = Snapshot with { Hooks = [.. Snapshot.Hooks.Select(static hook => hook with { IsConsented = true })] };
        }

        return ChangeAsync();
    }

    /// <inheritdoc />
    public Task RevokeAsync(string hookId, CancellationToken cancellationToken)
    {
        _calls.Add($"Revoke({hookId})");
        return ChangeHookAsync(hookId, static hook => hook with { IsConsented = false });
    }

    /// <inheritdoc />
    public Task LiftQuarantineAsync(string source, string reason, CancellationToken cancellationToken)
    {
        _calls.Add($"LiftQuarantine({source}, {reason})");
        if (ChangeFailure is null)
        {
            Snapshot = Snapshot with
            {
                Hooks = [.. Snapshot.Hooks.Select(hook => string.Equals(hook.QuarantinedSource, source, StringComparison.Ordinal) ? hook with { QuarantinedSource = null, QuarantineReason = null } : hook)],
            };
        }

        return ChangeAsync();
    }

    private Task ChangeHookAsync(string hookId, Func<HookItem, HookItem> change)
    {
        if (ChangeFailure is null)
        {
            Snapshot = Snapshot with { Hooks = [.. Snapshot.Hooks.Select(hook => string.Equals(hook.Id, hookId, StringComparison.Ordinal) ? change(hook) : hook)] };
        }

        return ChangeAsync();
    }

    private Task ChangeAsync() => ChangeFailure is { } failure ? Task.FromException(new SettingsSourceException(failure)) : Task.CompletedTask;
}
