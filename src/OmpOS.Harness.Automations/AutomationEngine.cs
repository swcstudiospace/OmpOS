using System.Collections.Immutable;

namespace OmpOS.Harness.Automations;

/// <summary>
/// Runs automations behind their guards, in this order: the enable toggle, the loop guards, pauses, plugin consent and the
/// rate limit. An admitted run then executes under its maximum runtime. Every decision, including each block and refusal,
/// is recorded and returned by <see cref="History"/>.
/// </summary>
/// <remarks>
/// A run starts only when every guard admits it. A guard store or consent gate that throws refuses the run, so a broken
/// guard never admits work. Schedules, rate windows and runtime bounds are evaluated on the injected clock.
/// </remarks>
public sealed class AutomationEngine
{
    /// <summary>The deepest chain of event-triggered runs. A run deeper than this is blocked.</summary>
    public const int MaxChainDepth = 3;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];
    private readonly IAutomationConsentGate _consent;
    private readonly IAutomationGuardStore _guards;
    private readonly TimeProvider _clock;
    private ImmutableList<AutomationRunRecord> _history = ImmutableList<AutomationRunRecord>.Empty;

    /// <summary>
    /// Creates an engine. Every automation starts disabled.
    /// </summary>
    /// <param name="definitions">The automations, in registration order.</param>
    /// <param name="consent">The plugin consent gate that must approve each automation's hook before it runs.</param>
    /// <param name="guards">The store of pauses and admitted-run history.</param>
    /// <param name="clock">The clock that evaluates schedules, rate windows and runtime bounds.</param>
    /// <exception cref="ArgumentException">Two automations share an identifier.</exception>
    public AutomationEngine(
        IEnumerable<AutomationDefinition> definitions,
        IAutomationConsentGate consent,
        IAutomationGuardStore guards,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(consent);
        ArgumentNullException.ThrowIfNull(guards);
        ArgumentNullException.ThrowIfNull(clock);
        _consent = consent;
        _guards = guards;
        _clock = clock;

        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                throw new ArgumentException("An automation definition is null.", nameof(definitions));
            }

            var firstDue = definition.Trigger is ScheduledTrigger scheduled
                ? scheduled.Schedule.FirstRunAt
                : DateTimeOffset.MaxValue;
            if (!_entries.TryAdd(definition.Id, new Entry(definition, firstDue)))
            {
                throw new ArgumentException($"More than one automation has the identifier '{definition.Id}'.", nameof(definitions));
            }

            _order.Add(definition.Id);
        }
    }

    /// <summary>
    /// Turns an automation on or off. Every automation starts off, and a disabled automation's runs are refused.
    /// </summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="enabled">True to enable the automation.</param>
    /// <exception cref="ArgumentException">No automation has the identifier.</exception>
    public void SetEnabled(string automationId, bool enabled)
    {
        lock (_gate)
        {
            FindLocked(automationId).Enabled = enabled;
        }
    }

    /// <summary>
    /// Returns true when the automation is enabled.
    /// </summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <exception cref="ArgumentException">No automation has the identifier.</exception>
    public bool IsEnabled(string automationId)
    {
        lock (_gate)
        {
            return FindLocked(automationId).Enabled;
        }
    }

    /// <summary>
    /// Pauses an automation. The pause names the guard that placed it and how to resume, and refuses every run until
    /// <see cref="ResumeAsync"/> clears it.
    /// </summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="pause">The pause.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentException">No automation has the identifier.</exception>
    public async Task PauseAsync(string automationId, AutomationPause pause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pause);
        RequireAutomation(automationId);
        await _guards.PauseAsync(automationId, pause, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the pause on an automation, so its runs are no longer refused by it.
    /// </summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentException">No automation has the identifier.</exception>
    public async Task ResumeAsync(string automationId, CancellationToken cancellationToken)
    {
        RequireAutomation(automationId);
        await _guards.ResumeAsync(automationId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Requests a run of an automation. A manual run is allowed for any automation, whatever its trigger.
    /// </summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the run. The body is cancelled and the run is recorded as cancelled.</param>
    /// <returns>The record of the attempt.</returns>
    /// <exception cref="ArgumentException">No automation has the identifier.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled during the run.</exception>
    public Task<AutomationRunRecord> RunManualAsync(string automationId, CancellationToken cancellationToken) =>
        RunAsync(automationId, RunStart.Manual(), cancellationToken);

    /// <summary>
    /// Dispatches an event to every event-triggered automation with its name, in registration order.
    /// </summary>
    /// <param name="automationEvent">The event and its origin.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>The record of every attempt, including those the guards blocked or refused.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled during a run.</exception>
    public async Task<IReadOnlyList<AutomationRunRecord>> DispatchEventAsync(AutomationEvent automationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(automationEvent);
        var results = new List<AutomationRunRecord>();
        foreach (var automationId in EventTargets(automationEvent.Name))
        {
            results.Add(await RunAsync(automationId, RunStart.ForEvent(automationEvent), cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <summary>
    /// Runs every scheduled automation whose due instant is at or before the injected clock's current time. Due instants
    /// missed in the meantime coalesce into one run, and the next due instant moves past the current time.
    /// </summary>
    /// <param name="cancellationToken">Cancels the runs.</param>
    /// <returns>The record of every attempt made.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled during a run.</exception>
    public async Task<IReadOnlyList<AutomationRunRecord>> RunDueScheduledAsync(CancellationToken cancellationToken)
    {
        var results = new List<AutomationRunRecord>();
        foreach (var automationId in TakeDue(_clock.GetUtcNow()))
        {
            results.Add(await RunAsync(automationId, RunStart.Scheduled(), cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <summary>
    /// Returns every decision the engine has recorded, in the order it made them.
    /// </summary>
    public IReadOnlyList<AutomationRunRecord> History()
    {
        lock (_gate)
        {
            return _history;
        }
    }

    private async Task<AutomationRunRecord> RunAsync(string automationId, RunStart start, CancellationToken cancellationToken)
    {
        var (definition, enabled) = Snapshot(automationId);
        if (!enabled)
        {
            return Record(start, definition.Id, AutomationRunOutcome.Refused, "enable-toggle", "the automation is disabled; enable it before it can run", null);
        }

        if (start.Event is { } automationEvent && string.Equals(automationEvent.Origin, definition.Id, StringComparison.Ordinal))
        {
            return Record(
                start,
                definition.Id,
                AutomationRunOutcome.Blocked,
                "loop-guard",
                $"self-trigger: event '{automationEvent.Name}' originates from this automation's own run",
                null);
        }

        if (start.Depth > MaxChainDepth)
        {
            return Record(
                start,
                definition.Id,
                AutomationRunOutcome.Blocked,
                "chain-depth",
                $"chain depth {start.Depth} exceeds the cap of {MaxChainDepth}",
                null);
        }

        var refusal = await AdmitAsync(definition, cancellationToken).ConfigureAwait(false);
        if (refusal is not null)
        {
            return Record(start, definition.Id, AutomationRunOutcome.Refused, refusal.Guard, refusal.Reason, refusal.Pause);
        }

        return await ExecuteAsync(definition, start, cancellationToken).ConfigureAwait(false);
    }

    // The guards in order: pause, consent, then rate limit. The first refusal is returned; null means every guard admitted the run.
    private async Task<Refusal?> AdmitAsync(AutomationDefinition definition, CancellationToken cancellationToken)
    {
        var refusal = await PauseRefusalAsync(definition.Id, cancellationToken).ConfigureAwait(false);
        refusal ??= await ConsentRefusalAsync(definition, cancellationToken).ConfigureAwait(false);
        return refusal ?? await RateRefusalAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Refusal?> PauseRefusalAsync(string automationId, CancellationToken cancellationToken)
    {
        var lookup = await AskAsync(() => _guards.FindPauseAsync(automationId, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (lookup.Fault is { } fault)
        {
            return StoreFailure(fault);
        }

        return lookup.Value is { } pause
            ? new Refusal(pause.Guard, $"paused by guard '{pause.Guard}': {pause.Reason}. To resume: {pause.HowToResume}", pause)
            : null;
    }

    private async Task<Refusal?> ConsentRefusalAsync(AutomationDefinition definition, CancellationToken cancellationToken)
    {
        var answer = await AskAsync(
            () => ValueTask.FromResult(_consent.IsConsented(definition.ConsentHookId)),
            cancellationToken).ConfigureAwait(false);
        if (answer.Fault is { } fault)
        {
            return new Refusal(
                "consent",
                $"the consent gate failed for hook '{definition.ConsentHookId}', so the run is refused: {fault}",
                null);
        }

        return answer.Value
            ? null
            : new Refusal(
                "consent",
                $"consent is not recorded for hook '{definition.ConsentHookId}'; grant it through the plugin consent gate before running",
                null);
    }

    // Counts the runs inside the window and, when admitted, records this run. A failed store refuses the run.
    private async Task<Refusal?> RateRefusalAsync(AutomationDefinition definition, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        if (definition.RateLimit is { } limit)
        {
            var counted = await AskAsync(
                () => _guards.CountRunsSinceAsync(definition.Id, now - limit.Window, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            if (counted.Fault is { } countFault)
            {
                return StoreFailure(countFault);
            }

            if (counted.Value >= limit.MaxRuns)
            {
                return new Refusal(
                    "rate-limit",
                    $"{counted.Value} runs started within the last {limit.Window}; the limit is {limit.MaxRuns} per window",
                    null);
            }
        }

        var recorded = await AskAsync(
            async () =>
            {
                await _guards.RecordRunAsync(definition.Id, now, cancellationToken).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        return recorded.Fault is { } recordFault ? StoreFailure(recordFault) : null;
    }

    // Runs the body under its maximum runtime. The bound and the caller's token both cancel the body; the run then waits for
    // the body to return, so a cancelled body never keeps running after its record is written.
    private async Task<AutomationRunRecord> ExecuteAsync(AutomationDefinition definition, RunStart start, CancellationToken cancellationToken)
    {
        var context = new AutomationRunContext(definition.Id, start.Depth, DispatchEventAsync);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var deadline = Task.Delay(definition.MaxRuntime, _clock, bound.Token);
        var body = RunBodyAsync(definition.Body, context, bound.Token);

        var finished = await Task.WhenAny(body, deadline).ConfigureAwait(false);
        var timedOut = finished == deadline;
        await bound.CancelAsync().ConfigureAwait(false);
        await Task.WhenAny(body).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            _ = Record(start, definition.Id, AutomationRunOutcome.Cancelled, null, "the caller cancelled the run", null);
            throw new OperationCanceledException(cancellationToken);
        }

        if (timedOut)
        {
            return Record(
                start,
                definition.Id,
                AutomationRunOutcome.TimedOut,
                "max-runtime",
                $"exceeded the maximum runtime of {definition.MaxRuntime}; the run was cancelled",
                null);
        }

        if (body.IsFaulted)
        {
            return Record(
                start,
                definition.Id,
                AutomationRunOutcome.Failed,
                null,
                $"the body failed: {body.Exception!.GetBaseException().Message}",
                null);
        }

        if (body.IsCanceled)
        {
            return Record(start, definition.Id, AutomationRunOutcome.Failed, null, "the body cancelled itself before its runtime bound", null);
        }

        return Record(start, definition.Id, AutomationRunOutcome.Succeeded, null, "the body completed", null);
    }

    private static async Task RunBodyAsync(AutomationBody body, AutomationRunContext context, CancellationToken cancellationToken) =>
        await body(context, cancellationToken).ConfigureAwait(false);

    // Calls a guard and reports how it ended. A call that throws, whether synchronously or not, becomes a fault here: an
    // async method captures a synchronous throw in its task, so the engine reads the failure without a catch-all clause.
    private static async Task<Answer<T>> AskAsync<T>(Func<ValueTask<T>> call, CancellationToken cancellationToken)
    {
        var pending = CallAsync(call);
        await Task.WhenAny(pending).ConfigureAwait(false);
        if (pending.IsCompletedSuccessfully)
        {
            return new Answer<T>(await pending.ConfigureAwait(false), null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new Answer<T>(default, pending.Exception?.GetBaseException().Message ?? "the call did not complete");
    }

    private static async Task<T> CallAsync<T>(Func<ValueTask<T>> call) => await call().ConfigureAwait(false);

    private static Refusal StoreFailure(string fault) =>
        new("guard-store", $"the guard store failed, so the run is refused: {fault}", null);

    private AutomationRunRecord Record(
        RunStart start,
        string automationId,
        AutomationRunOutcome outcome,
        string? guard,
        string reason,
        AutomationPause? pause)
    {
        var record = new AutomationRunRecord(automationId, start.Depth, start.Origin, outcome, guard, reason, pause, _clock.GetUtcNow());
        lock (_gate)
        {
            _history = _history.Add(record);
        }

        return record;
    }

    private (AutomationDefinition Definition, bool Enabled) Snapshot(string automationId)
    {
        lock (_gate)
        {
            var entry = FindLocked(automationId);
            return (entry.Definition, entry.Enabled);
        }
    }

    private List<string> EventTargets(string eventName)
    {
        lock (_gate)
        {
            return _order
                .Where(id => _entries[id].Definition.Trigger is EventTrigger trigger && string.Equals(trigger.EventName, eventName, StringComparison.Ordinal))
                .ToList();
        }
    }

    private List<string> TakeDue(DateTimeOffset now)
    {
        lock (_gate)
        {
            var due = new List<string>();
            foreach (var id in _order)
            {
                var entry = _entries[id];
                if (entry.Definition.Trigger is not ScheduledTrigger scheduled || entry.NextDue > now)
                {
                    continue;
                }

                var interval = scheduled.Schedule.Interval.Ticks;
                var missed = (now - entry.NextDue).Ticks / interval + 1;
                entry.NextDue = entry.NextDue.AddTicks(interval * missed);
                due.Add(id);
            }

            return due;
        }
    }

    private void RequireAutomation(string automationId)
    {
        lock (_gate)
        {
            _ = FindLocked(automationId);
        }
    }

    private Entry FindLocked(string automationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        return _entries.TryGetValue(automationId, out var entry)
            ? entry
            : throw new ArgumentException($"No automation has the identifier '{automationId}'.", nameof(automationId));
    }

    private sealed class Entry(AutomationDefinition definition, DateTimeOffset nextDue)
    {
        public AutomationDefinition Definition { get; } = definition;

        public bool Enabled { get; set; }

        public DateTimeOffset NextDue { get; set; } = nextDue;
    }

    private sealed record Answer<T>(T? Value, string? Fault);

    private sealed record Refusal(string Guard, string Reason, AutomationPause? Pause);

    private sealed record RunStart(string Origin, int Depth, AutomationEvent? Event)
    {
        public static RunStart Manual() => new("manual", 1, null);

        public static RunStart Scheduled() => new("schedule", 1, null);

        public static RunStart ForEvent(AutomationEvent automationEvent) =>
            new(automationEvent.Origin, automationEvent.EmittedAtDepth + 1, automationEvent);
    }
}
