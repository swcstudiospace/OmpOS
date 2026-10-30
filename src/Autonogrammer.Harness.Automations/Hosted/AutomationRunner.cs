using System.Collections.Concurrent;

namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Starts automation runs. It admits a run only when the automation's guards allow it (concurrency limit, runs-per-hour cap), writes
/// the run to the history, executes the matching <see cref="IAutomationTarget"/> in the background under the automation's maximum
/// runtime, and records how it ended. A refused run is recorded as <see cref="AutomationRunStatus.Skipped"/>. The runner never throws
/// for a failing target: the failure becomes the run's history entry, without the exception message, which could hold a secret.
/// </summary>
public sealed class AutomationRunner : IAsyncDisposable
{
    private readonly IAutomationStore _store;
    private readonly Dictionary<AutomationTargetKind, IAutomationTarget> _targets;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _admission = new(1, 1);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _active = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task> _running = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _shutdown = new();
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="AutomationRunner"/> class.</summary>
    /// <param name="store">The automation store that holds the run history.</param>
    /// <param name="targets">The targets, at most one per kind.</param>
    /// <param name="clock">The clock that stamps runs, counts the hour and times the maximum runtime.</param>
    /// <exception cref="ArgumentException">Two targets have the same kind.</exception>
    public AutomationRunner(IAutomationStore store, IEnumerable<IAutomationTarget> targets, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _clock = clock;
        _targets = [];
        foreach (var target in targets)
        {
            if (!_targets.TryAdd(target.Kind, target))
            {
                throw new ArgumentException($"More than one automation target has the kind '{target.Kind}'.", nameof(targets));
            }
        }
    }

    /// <summary>Gets whether a target of a kind is registered on this host.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>True when a target is registered.</returns>
    public bool HasTarget(AutomationTargetKind kind) => _targets.ContainsKey(kind);

    /// <summary>Asks for a run. The run is admitted or refused at once; an admitted run continues in the background.</summary>
    /// <param name="automation">The automation to run. It need not be enabled: the caller decides whether a trigger may start it.</param>
    /// <param name="trigger">What started the run.</param>
    /// <param name="cancellationToken">Cancels the admission only, not the run.</param>
    /// <returns>The admission result.</returns>
    public async Task<AutomationRunStart> StartAsync(UserAutomation automation, AutomationTriggerContext trigger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(automation);
        ArgumentNullException.ThrowIfNull(trigger);
        try
        {
            await _admission.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return AutomationRunStart.Refused(AutomationRunRefusal.ShuttingDown);
        }

        try
        {
            if (_disposed)
            {
                return AutomationRunStart.Refused(AutomationRunRefusal.ShuttingDown);
            }

            var now = _clock.GetUtcNow();
            var runId = Guid.NewGuid().ToString("N");
            var history = await _store.GetHistoryAsync(automation.Id, cancellationToken).ConfigureAwait(false);
            var refusal = Check(automation, history, now);
            if (refusal is { } refused)
            {
                await _store.UpsertRunAsync(automation.Id, SkippedEntry(runId, trigger, now, refused), cancellationToken).ConfigureAwait(false);
                return AutomationRunStart.Refused(refused);
            }

            var entry = new AutomationRunEntry
            {
                RunId = runId,
                Source = trigger.Source,
                Summary = trigger.Summary,
                Status = AutomationRunStatus.Running,
                StartedAt = now,
            };
            await _store.UpsertRunAsync(automation.Id, entry, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                _active[automation.Id] = _active.GetValueOrDefault(automation.Id) + 1;
            }

            var task = Task.Run(() => ExecuteAsync(automation, entry, trigger), CancellationToken.None);
            _running[runId] = task;
            _ = task.ContinueWith(
                _ => _running.TryRemove(new KeyValuePair<string, Task>(runId, task)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return AutomationRunStart.Started(runId, task);
        }
        finally
        {
            _ = _admission.Release();
        }
    }

    /// <summary>
    /// Marks every run that the history still shows as running, and that this runner does not execute, as failed with an interruption
    /// error. Call it once when the host starts, before any trigger fires.
    /// </summary>
    /// <param name="cancellationToken">Cancels the recovery.</param>
    /// <returns>The number of runs marked.</returns>
    public async Task<int> RecoverInterruptedRunsAsync(CancellationToken cancellationToken)
    {
        var marked = 0;
        foreach (var automation in await _store.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            var history = await _store.GetHistoryAsync(automation.Id, cancellationToken).ConfigureAwait(false);
            foreach (var entry in history.Where(candidate => candidate.Status == AutomationRunStatus.Running && !_running.ContainsKey(candidate.RunId)))
            {
                var now = _clock.GetUtcNow();
                await _store.UpsertRunAsync(
                    automation.Id,
                    entry with { Status = AutomationRunStatus.Failed, FinishedAt = now, Error = "The run was interrupted because the host stopped." },
                    cancellationToken).ConfigureAwait(false);
                marked++;
            }
        }

        return marked;
    }

    /// <summary>Cancels the runs in flight and waits for them to be recorded.</summary>
    /// <returns>A task that completes when every run has ended.</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _admission.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
        finally
        {
            _ = _admission.Release();
        }

        await _shutdown.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(_running.Values).ConfigureAwait(false);
        _shutdown.Dispose();
        _admission.Dispose();
    }

    private static AutomationRunEntry SkippedEntry(string runId, AutomationTriggerContext trigger, DateTimeOffset now, AutomationRunRefusal refusal) =>
        new()
        {
            RunId = runId,
            Source = trigger.Source,
            Summary = trigger.Summary,
            Status = AutomationRunStatus.Skipped,
            StartedAt = now,
            FinishedAt = now,
            Error = refusal switch
            {
                AutomationRunRefusal.ConcurrencyLimit => "Skipped: the automation already has its maximum number of runs in progress.",
                AutomationRunRefusal.HourlyCap => "Skipped: the automation reached its runs-per-hour cap.",
                _ => "Skipped: the host is stopping.",
            },
        };

    private static string Describe(Exception exception) => $"The target failed unexpectedly ({exception.GetType().Name}).";

    private AutomationRunRefusal? Check(UserAutomation automation, IReadOnlyList<AutomationRunEntry> history, DateTimeOffset now)
    {
        int active;
        lock (_gate)
        {
            active = _active.GetValueOrDefault(automation.Id);
        }

        if (active >= automation.MaxConcurrentRuns)
        {
            return AutomationRunRefusal.ConcurrencyLimit;
        }

        var since = now - TimeSpan.FromHours(1);
        var lastHour = history.Count(entry => entry.CountsAgainstHourlyCap && entry.StartedAt > since);
        return lastHour >= automation.MaxRunsPerHour ? AutomationRunRefusal.HourlyCap : null;
    }

    private async Task<AutomationRunEntry> ExecuteAsync(UserAutomation automation, AutomationRunEntry started, AutomationTriggerContext trigger)
    {
        AutomationRunEntry final;
        try
        {
            final = await RunTargetAsync(automation, started, trigger).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                var remaining = _active.GetValueOrDefault(automation.Id) - 1;
                if (remaining <= 0)
                {
                    _ = _active.Remove(automation.Id);
                }
                else
                {
                    _active[automation.Id] = remaining;
                }
            }
        }

        try
        {
            await _store.UpsertRunAsync(automation.Id, final, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The outcome is returned to the awaiting caller; the history keeps the earlier Running entry, which recovery marks on restart.
        }

        return final;
    }

    private async Task<AutomationRunEntry> RunTargetAsync(UserAutomation automation, AutomationRunEntry started, AutomationTriggerContext trigger)
    {
        if (!_targets.TryGetValue(automation.Target.Kind, out var target))
        {
            return Finish(started, AutomationRunStatus.Failed, null, $"No {automation.Target.Kind} target is available on this host.");
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(automation.MaxRuntimeMinutes), _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, _shutdown.Token);
        var request = new AutomationRunRequest(automation, started.RunId, AutomationPromptBuilder.Build(automation, trigger), trigger);
        try
        {
            var result = await target.RunAsync(request, linked.Token).ConfigureAwait(false);
            var status = result.Status is AutomationRunStatus.Succeeded or AutomationRunStatus.Launched ? result.Status : AutomationRunStatus.Failed;
            return Finish(started, status, result.ExternalId, status == AutomationRunStatus.Failed ? result.Error ?? "The target reported a failure." : null);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return Finish(started, AutomationRunStatus.TimedOut, null, $"The run exceeded its maximum runtime of {automation.MaxRuntimeMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)} minutes.");
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            return Finish(started, AutomationRunStatus.Cancelled, null, "The run was cancelled because the host stopped.");
        }
        catch (OperationCanceledException)
        {
            return Finish(started, AutomationRunStatus.Cancelled, null, "The target cancelled the run.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Finish(started, AutomationRunStatus.Failed, null, Describe(exception));
        }
    }

    private AutomationRunEntry Finish(AutomationRunEntry started, AutomationRunStatus status, string? externalId, string? error) =>
        started with
        {
            Status = status,
            FinishedAt = _clock.GetUtcNow(),
            ExternalId = externalId,
            Error = error is null ? null : error.Length <= AutomationLimits.MaxErrorLength ? error : error[..AutomationLimits.MaxErrorLength],
        };
}
