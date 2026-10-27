using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Runs the hooks of an event and merges their answers. For every hook, in merge order: it applies the enabled flag, the
/// <c>runsIn</c> filter and the matcher, then checks quarantine and consent BEFORE anything is spawned (ADR-0013 decision 2),
/// then runs the hook and applies the failure policy. All matching hooks run, one after the other; the answers merge with
/// deny beating ask beating allow. Hooks of <c>stop</c> and <c>subagentStop</c> are bounded by their <c>loop_limit</c>. While a
/// prompt hook's model call runs, every dispatch in its async flow returns at once, so a prompt hook cannot recurse.
/// </summary>
public sealed class HookDispatcher
{
    private static readonly AsyncLocal<int> PromptDepth = new();

    private readonly PluginStateStore _state;
    private readonly IHookCommandRunner _runner;
    private readonly HookRunLog? _log;
    private readonly IPromptHookEvaluator? _prompts;
    private readonly TimeProvider _clock;
    private readonly HookDispatcherOptions _options;
    private EffectiveHookSet _hooks;

    /// <summary>
    /// Initializes a dispatcher.
    /// </summary>
    /// <param name="hooks">The effective hook set to start with.</param>
    /// <param name="state">The consent and quarantine state.</param>
    /// <param name="runner">Starts each hook process. Tests pass a fake that counts spawns.</param>
    /// <param name="log">The run log, or null for none.</param>
    /// <param name="prompts">Evaluates prompt hooks, or null when there is no model access (prompt hooks then fail).</param>
    /// <param name="clock">The clock for timestamps and durations; the system clock when null.</param>
    /// <param name="options">Limits and policy; the defaults when null.</param>
    public HookDispatcher(
        EffectiveHookSet hooks,
        PluginStateStore state,
        IHookCommandRunner runner,
        HookRunLog? log = null,
        IPromptHookEvaluator? prompts = null,
        TimeProvider? clock = null,
        HookDispatcherOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(runner);
        _hooks = hooks;
        _state = state;
        _runner = runner;
        _log = log;
        _prompts = prompts;
        _clock = clock ?? TimeProvider.System;
        _options = options ?? new HookDispatcherOptions();
    }

    /// <summary>The hook set in force.</summary>
    public EffectiveHookSet Hooks => Volatile.Read(ref _hooks);

    /// <summary>
    /// Swaps in a freshly loaded hook set. A dispatch that already started keeps the set it began with.
    /// </summary>
    /// <param name="hooks">The new set.</param>
    public void Replace(EffectiveHookSet hooks)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        Volatile.Write(ref _hooks, hooks);
    }

    /// <summary>
    /// Dispatches a Cursor event.
    /// </summary>
    /// <param name="hookEvent">The event.</param>
    /// <param name="session">The conversation the event belongs to.</param>
    /// <param name="payload">The event-specific fields as a JSON object.</param>
    /// <param name="cancellationToken">Cancels the dispatch; the running hook's process tree is killed.</param>
    /// <returns>The merged decision. When no hook applies it is <see cref="HookDecision.None"/>.</returns>
    public Task<HookDecision> DispatchAsync(HookEvent hookEvent, HookSession session, JsonElement payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var info = HookEvents.Info(hookEvent);
        var context = new HookDispatchContext(info.Name, hookEvent, info, session, payload, HookEvents.MatcherValue(hookEvent, payload));
        return DispatchCoreAsync(context, Hooks.For(hookEvent), cancellationToken);
    }

    /// <summary>
    /// Dispatches an omp extension event to the hooks of the <c>omp</c> section. Each hook receives the event object plus the
    /// common fields and answers with the omp result object, which the dispatcher merges.
    /// </summary>
    /// <param name="ompEvent">The omp event name, for example <c>tool_call</c>.</param>
    /// <param name="session">The conversation the event belongs to.</param>
    /// <param name="payload">The omp event object as JSON.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>The merged decision; <see cref="HookDecision.OmpResult"/> holds the merged result object.</returns>
    public Task<HookDecision> DispatchOmpAsync(string ompEvent, HookSession session, JsonElement payload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ompEvent);
        ArgumentNullException.ThrowIfNull(session);
        var context = new HookDispatchContext(ompEvent, null, null, session, payload, OmpMatcherValue(ompEvent, payload));
        return DispatchCoreAsync(context, Hooks.ForOmp(ompEvent), cancellationToken);
    }

    /// <summary>
    /// Dispatches by name, as the loopback bridge receives it.
    /// </summary>
    /// <param name="eventName">A Cursor event name, or an omp event name when <paramref name="isOmpEvent"/> is true.</param>
    /// <param name="isOmpEvent">True when <paramref name="eventName"/> is an omp extension event.</param>
    /// <param name="session">The conversation.</param>
    /// <param name="payload">The payload object.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>The merged decision; <see cref="HookDecision.None"/> for an unknown Cursor event name.</returns>
    public Task<HookDecision> DispatchNamedAsync(string eventName, bool isOmpEvent, HookSession session, JsonElement payload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (isOmpEvent)
        {
            return DispatchOmpAsync(eventName, session, payload, cancellationToken);
        }

        return HookEvents.TryParse(eventName, out var hookEvent)
            ? DispatchAsync(hookEvent, session, payload, cancellationToken)
            : Task.FromResult(HookDecision.None);
    }

    private static string? OmpMatcherValue(string ompEvent, JsonElement payload)
    {
        var field = ompEvent switch
        {
            "tool_call" or "tool_result" => "toolName",
            "before_subagent_spawn" => "agent",
            _ => null,
        };
        return field is not null && payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private async Task<HookDecision> DispatchCoreAsync(HookDispatchContext context, IReadOnlyList<EffectiveHook> hooks, CancellationToken cancellationToken)
    {
        if (PromptDepth.Value > 0 || hooks.Count == 0)
        {
            return HookDecision.None;
        }

        var builder = new HookDecisionBuilder();
        foreach (var hook in hooks)
        {
            var step = await RunHookAsync(hook, context, cancellationToken).ConfigureAwait(false);
            if (step is null)
            {
                continue;
            }

            builder.Add(step);
            if (step.DenyCause != HookDecisionCause.None && context.Event is null)
            {
                AddOmpDeny(builder.Omp, context.EventName, step.DenyAgentMessage);
            }

            if (_log is not null)
            {
                await _log.AppendAsync(step.Record, cancellationToken).ConfigureAwait(false);
            }
        }

        var decision = builder.Build();
        if (context.Event == HookEvent.SessionStart)
        {
            context.Session.MergeEnvironment(decision.Environment);
        }

        return decision;
    }

    private async Task<HookStep?> RunHookAsync(EffectiveHook hook, HookDispatchContext context, CancellationToken cancellationToken)
    {
        if (!hook.Enabled || _state.IsHookDisabled(hook.Id))
        {
            return null;
        }

        var required = context.Session.IsRemote ? HookLocations.Remote : HookLocations.Local;
        if ((hook.RunsIn & required) == HookLocations.None)
        {
            return null;
        }

        var now = _clock.GetUtcNow();
        if (hook.MatcherProblem is not null)
        {
            var deny = hook.FailClosed && context.CanBlock;
            var message = deny ? $"Hook {hook.Id} is failClosed and its matcher is not a valid regular expression." : null;
            return HookStepFactory.NotRun(hook, context, now, HookRunStatus.Skipped, $"invalid matcher: {hook.MatcherProblem}", deny ? HookDecisionCause.InvalidMatcher : HookDecisionCause.None, message);
        }

        if (!HookMatcher.Matches(hook.Matcher, hook.CompiledMatcher, context.MatcherValue))
        {
            return null;
        }

        if (_state.FindQuarantine(hook.PluginId) is { } quarantine)
        {
            return hook.Blocking
                ? HookStepFactory.NotRun(hook, context, now, HookRunStatus.PluginQuarantined, $"quarantined: {quarantine.Reason}", HookDecisionCause.PluginQuarantined, $"Hook {hook.Id} guards this action but its source '{hook.PluginId}' is quarantined: {quarantine.Reason}")
                : HookStepFactory.NotRun(hook, context, now, HookRunStatus.Skipped, $"quarantined: {quarantine.Reason}");
        }

        if (!hook.PreConsented && !_state.IsConsented(hook.CommandHash))
        {
            return hook.Blocking
                ? HookStepFactory.NotRun(hook, context, now, HookRunStatus.AwaitingConsent, "no consent for this hook", HookDecisionCause.ConsentMissing, $"Hook {hook.Id} guards this action and has no consent. Review it on the Hooks page and grant consent.")
                : HookStepFactory.NotRun(hook, context, now, HookRunStatus.AwaitingConsent, "no consent for this hook");
        }

        return await ExecuteAsync(hook, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HookStep> ExecuteAsync(EffectiveHook hook, HookDispatchContext context, CancellationToken cancellationToken)
    {
        var session = context.Session;
        var loopCount = context.HasFollowups ? session.LoopCount(hook.Id) : (int?)null;
        var input = HookInputBuilder.Build(context.EventName, session, context.Payload, loopCount);
        var sha = Convert.ToHexStringLower(SHA256.HashData(input));
        var started = _clock.GetTimestamp();
        if (input.Length > _options.MaxInputBytes)
        {
            return HookOutcomeInterpreter.StartFailure(hook, context, HookRunStatus.Failed, HookDecisionCause.FailClosedFailure, $"its input is {input.Length} bytes, over the {_options.MaxInputBytes} byte limit", TimeSpan.Zero, sha, _clock.GetUtcNow());
        }

        var step = hook.Type == HookType.Prompt
            ? await RunPromptAsync(hook, context, input, sha, started, cancellationToken).ConfigureAwait(false)
            : await RunCommandAsync(hook, context, input, sha, started, cancellationToken).ConfigureAwait(false);
        return context.HasFollowups ? ApplyLoopLimit(hook, context, step) : step;
    }

    private async Task<HookStep> RunCommandAsync(
        EffectiveHook hook,
        HookDispatchContext context,
        byte[] input,
        string sha,
        long started,
        CancellationToken cancellationToken)
    {
        var parent = _options.ParentEnvironment ?? HookEnvironment.CaptureParent();
        var environment = HookEnvironment.Build(parent, _options.InheritEnvironment, hook, context.Session, context.EventName);
        var invocation = new HookInvocation(
            hook.Command,
            hook.Args,
            hook.Shell,
            hook.WorkingDirectory,
            environment,
            input,
            _options.MaxStandardOutputBytes,
            _options.MaxStandardErrorBytes);

        using var timeout = new CancellationTokenSource(hook.Timeout, _clock);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        // Task.Run turns a synchronous throw from the runner into a faulted task, so every runner failure takes one path.
        var running = Task.Run(() => _runner.RunAsync(invocation, limit.Token), CancellationToken.None);
        var fault = await TaskFault.AwaitAsync(running).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var elapsed = _clock.GetElapsedTime(started);
        var at = _clock.GetUtcNow();

        if (fault is OperationCanceledException && timeout.IsCancellationRequested)
        {
            return HookOutcomeInterpreter.StartFailure(hook, context, HookRunStatus.TimedOut, HookDecisionCause.FailClosedTimeout, $"it ran past its {(int)hook.Timeout.TotalSeconds} second timeout", elapsed, sha, at);
        }

        if (fault is not null)
        {
            return await FaultAsync(hook, context, FaultReason.Describe($"Hook '{hook.Id}'", fault), elapsed, sha, at, cancellationToken).ConfigureAwait(false);
        }

        var outcome = await running.ConfigureAwait(false);
        if (outcome.SpawnFailure == HookSpawnFailure.None)
        {
            return HookOutcomeInterpreter.Interpret(hook, context, outcome, elapsed, sha, at);
        }

        if (hook.Shell != HookShell.None && outcome.SpawnFailure == HookSpawnFailure.ExecutableNotFound)
        {
            // The shell itself is missing: the host is broken, not the hook.
            return await FaultAsync(hook, context, $"Hook '{hook.Id}' could not start its shell: {outcome.SpawnMessage}", elapsed, sha, at, cancellationToken).ConfigureAwait(false);
        }

        return HookOutcomeInterpreter.StartFailure(hook, context, HookRunStatus.Failed, HookDecisionCause.FailClosedFailure, $"it could not start ({outcome.SpawnFailure}): {outcome.SpawnMessage}", elapsed, sha, at);
    }

    private async Task<HookStep> FaultAsync(
        EffectiveHook hook,
        HookDispatchContext context,
        string reason,
        TimeSpan elapsed,
        string sha,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // The runner failed, so the source that owns the hook is quarantined. Other sources keep running.
        await _state.QuarantineAsync(hook.PluginId, reason, cancellationToken).ConfigureAwait(false);
        var deny = hook.Blocking;
        var message = deny ? $"Hook {hook.Id} guards this action but its runner failed, so '{hook.PluginId}' is quarantined." : null;
        return new HookStep(
            HookStepFactory.Record(hook, context, at, HookRunStatus.Faulted, deny ? "deny" : "none", null, elapsed, message, null, null, false, sha, reason),
            null,
            deny ? HookDecisionCause.RunnerFault : HookDecisionCause.None,
            message,
            message,
            true);
    }

    private async Task<HookStep> RunPromptAsync(
        EffectiveHook hook,
        HookDispatchContext context,
        byte[] input,
        string sha,
        long started,
        CancellationToken cancellationToken)
    {
        if (_prompts is not { } prompts)
        {
            return HookOutcomeInterpreter.StartFailure(hook, context, HookRunStatus.Failed, HookDecisionCause.FailClosedFailure, "no model is available to evaluate a prompt hook", _clock.GetElapsedTime(started), sha, _clock.GetUtcNow());
        }

        var text = HookPromptText.Build(hook.Prompt ?? string.Empty, Encoding.UTF8.GetString(input));
        using var timeout = new CancellationTokenSource(hook.Timeout, _clock);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        // The model call runs with hooks disabled: PromptDepth flows into the task started here and is restored at once.
        var previousDepth = PromptDepth.Value;
        PromptDepth.Value = previousDepth + 1;
        Task<PromptHookAnswer> running;
        try
        {
            running = Task.Run(() => prompts.EvaluateAsync(text, hook.Model, limit.Token).AsTask(), CancellationToken.None);
        }
        finally
        {
            PromptDepth.Value = previousDepth;
        }

        var fault = await TaskFault.AwaitAsync(running).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var elapsed = _clock.GetElapsedTime(started);
        var at = _clock.GetUtcNow();
        if (fault is not null)
        {
            var timedOut = fault is OperationCanceledException;
            return HookOutcomeInterpreter.StartFailure(
                hook,
                context,
                timedOut ? HookRunStatus.TimedOut : HookRunStatus.Failed,
                timedOut ? HookDecisionCause.FailClosedTimeout : HookDecisionCause.FailClosedFailure,
                timedOut ? "the model did not answer in time" : $"the model call failed: {fault.GetType().Name}",
                elapsed,
                sha,
                at);
        }

        var answer = await running.ConfigureAwait(false);
        var reason = answer.Reason ?? (answer.Ok ? null : "Denied by a prompt hook.");
        var raw = new HookOutputParse
        {
            Kind = HookOutputKind.Valid,
            Verdict = answer.Ok ? HookVerdict.Allow : HookVerdict.Deny,
            UserMessage = answer.Ok ? null : reason,
            AgentMessage = answer.Ok ? null : reason,
        };
        var output = context.Info is { } info ? HookOutputParser.Mask(raw, info.Honoured) : raw;
        return new HookStep(
            HookStepFactory.Record(hook, context, at, answer.Ok ? HookRunStatus.Succeeded : HookRunStatus.Denied, answer.Ok ? "allow" : "deny", null, elapsed, reason, null, null, false, sha, "prompt hook"),
            output,
            HookDecisionCause.None,
            null,
            null,
            true);
    }

    private static HookStep ApplyLoopLimit(EffectiveHook hook, HookDispatchContext context, HookStep step)
    {
        var session = context.Session;
        if (step.Output is not { FollowupMessage.Length: > 0 } output)
        {
            if (step.Ran && step.DenyCause == HookDecisionCause.None)
            {
                session.SetLoopCount(hook.Id, 0);
            }

            return step;
        }

        var count = session.LoopCount(hook.Id);
        if (context.Event == HookEvent.SubagentStop && !PayloadStatusIs(context.Payload, "completed"))
        {
            return Drop(step, output, "follow-up ignored: the subagent did not complete");
        }

        if (hook.LoopLimit is { } limit && count >= limit)
        {
            return Drop(step, output, $"loop limit reached ({count} of {limit}); follow-up dropped");
        }

        session.SetLoopCount(hook.Id, count + 1);
        return step;
    }

    private static HookStep Drop(HookStep step, HookOutputParse output, string note) =>
        step with
        {
            Output = output with { FollowupMessage = null },
            Record = step.Record with { Note = note },
        };

    private static bool PayloadStatusIs(JsonElement payload, string expected) =>
        payload.ValueKind == JsonValueKind.Object
        && payload.TryGetProperty("status", out var status)
        && status.ValueKind == JsonValueKind.String
        && string.Equals(status.GetString(), expected, StringComparison.Ordinal);

    private static void AddOmpDeny(HookOmpResultMerger merger, string ompEvent, string? message)
    {
        switch (ompEvent)
        {
            case "tool_call":
            case "before_subagent_spawn":
                merger.SetFlag("block", true);
                merger.SetText("reason", message ?? "Blocked by an OmpOS hook.");
                break;
            case "input":
                merger.SetFlag("handled", true);
                break;
            case "session_before_compact":
                merger.SetFlag("cancel", true);
                break;
            default:
                break;
        }
    }
}
