using System.Globalization;
using OmpOS.Harness.Automations.Cron;

namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Starts runs for cron triggers. Each pass looks, for every enabled automation, at the cron occurrences that came due since the
/// automation's stored cursor and applies the misfire policy: the most recent due occurrence runs once when it is at most
/// <see cref="AutomationLimits.MisfireWindow"/> late, older ones are folded into it, and an occurrence that is later than the window is
/// skipped and recorded as <see cref="AutomationRunStatus.Skipped"/>. A run never starts before its occurrence. The cursor then moves
/// to the current time, so a disabled automation does not catch up when it is enabled. The scheduler holds no timer of its own beyond
/// <see cref="RunAsync"/>: a test calls <see cref="TickAsync"/> after moving its clock.
/// </summary>
public sealed class AutomationScheduler
{
    /// <summary>How often <see cref="RunAsync"/> looks for due occurrences.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);

    private readonly IAutomationStore _store;
    private readonly AutomationRunner _runner;
    private readonly TimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="AutomationScheduler"/> class.</summary>
    /// <param name="store">The automation store.</param>
    /// <param name="runner">The runner that starts the runs.</param>
    /// <param name="clock">The clock.</param>
    public AutomationScheduler(IAutomationStore store, AutomationRunner runner, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _runner = runner;
        _clock = clock;
    }

    /// <summary>Looks at every automation once and starts the runs that are due.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>The number of runs admitted.</returns>
    public async Task<int> TickAsync(CancellationToken cancellationToken)
    {
        var started = 0;
        foreach (var automation in await _store.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = _clock.GetUtcNow();
            var cursor = await _store.GetScheduleCursorAsync(automation.Id, cancellationToken).ConfigureAwait(false) ?? automation.CreatedAt;
            if (cursor >= now)
            {
                continue;
            }

            if (automation.Enabled && FindLatestDue(automation, cursor, now) is { } due)
            {
                started += await FireAsync(automation, due, now, cancellationToken).ConfigureAwait(false);
            }

            await _store.SetScheduleCursorAsync(automation.Id, now, cancellationToken).ConfigureAwait(false);
        }

        return started;
    }

    /// <summary>Calls <see cref="TickAsync"/> now and then every <see cref="TickInterval"/> until cancelled.</summary>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <returns>A task that completes when the loop stops. Cancellation ends it without an exception.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TickInterval, _clock);
        try
        {
            do
            {
                _ = await TickAsync(cancellationToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // The host is stopping.
        }
    }

    private static DateTimeOffset? FindLatestDue(UserAutomation automation, DateTimeOffset cursor, DateTimeOffset now)
    {
        if (!AutomationTimeZones.TryResolve(automation.TimeZone, out var zone))
        {
            return null;
        }

        DateTimeOffset? latest = null;
        foreach (var trigger in automation.Triggers)
        {
            if (trigger.Kind != AutomationTriggerKind.Cron || !CronExpression.TryParse(trigger.Cron, out var expression, out _))
            {
                continue;
            }

            var cursorForTrigger = cursor;
            for (var step = 0; step < CronExpression.MaxOccurrences; step++)
            {
                if (expression.GetNextOccurrence(cursorForTrigger, zone) is not { } occurrence || occurrence > now)
                {
                    break;
                }

                latest = latest is null || occurrence > latest.Value ? occurrence : latest;
                cursorForTrigger = occurrence;
            }
        }

        return latest;
    }

    private async Task<int> FireAsync(UserAutomation automation, DateTimeOffset due, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (now - due > AutomationLimits.MisfireWindow)
        {
            var missed = new AutomationRunEntry
            {
                RunId = Guid.NewGuid().ToString("N"),
                Source = AutomationRunSource.Cron,
                Summary = "Scheduled run",
                Status = AutomationRunStatus.Skipped,
                StartedAt = now,
                FinishedAt = now,
                Error = $"Skipped: the occurrence due at {due.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC was more than 5 minutes late.",
            };
            await _store.UpsertRunAsync(automation.Id, missed, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        var start = await _runner.StartAsync(automation, AutomationTriggerContext.Cron(due), cancellationToken).ConfigureAwait(false);
        return start.Admitted ? 1 : 0;
    }
}
