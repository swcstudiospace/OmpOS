namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Applies the failure policy (design 3.5) to the result of one hook process: exit code 2 denies, exit 0 uses a valid
/// answer, and everything else is a failure that is fail-open unless the hook is <c>failClosed</c> on an event that can block.
/// </summary>
internal static class HookOutcomeInterpreter
{
    private const int DenyExitCode = 2;

    /// <summary>
    /// Interprets the outcome of a process that started.
    /// </summary>
    public static HookStep Interpret(
        EffectiveHook hook,
        HookDispatchContext context,
        HookCommandOutcome outcome,
        TimeSpan duration,
        string payloadSha256,
        DateTimeOffset at)
    {
        if (outcome.ExitCode == DenyExitCode && context.CanBlock)
        {
            return ExitCodeTwo(hook, context, outcome, duration, payloadSha256, at);
        }

        if (outcome.ExitCode != 0)
        {
            return Failure(hook, context, outcome, duration, payloadSha256, at, $"exit code {outcome.ExitCode}", HookDecisionCause.FailClosedFailure);
        }

        var parsed = HookOutputParser.Parse(outcome.StandardOutput, context.Event);
        switch (parsed.Kind)
        {
            case HookOutputKind.Empty:
                return Empty(hook, context, outcome, duration, payloadSha256, at);
            case HookOutputKind.Invalid when outcome.StandardOutputTruncated:
                return Failure(hook, context, outcome, duration, payloadSha256, at, "its output was cut at the capture limit before it could be parsed", HookDecisionCause.FailClosedFailure);
            case HookOutputKind.Invalid:
                return Invalid(hook, context, outcome, duration, payloadSha256, at, parsed.Problem ?? "invalid output");
            default:
                return Valid(hook, context, outcome, duration, payloadSha256, at, parsed);
        }
    }

    /// <summary>
    /// Creates the step of a hook that could not be started or that was cut off.
    /// </summary>
    public static HookStep StartFailure(
        EffectiveHook hook,
        HookDispatchContext context,
        HookRunStatus status,
        HookDecisionCause failClosedCause,
        string reason,
        TimeSpan duration,
        string? payloadSha256,
        DateTimeOffset at)
    {
        var deny = FailsClosed(hook, context);
        var message = deny ? $"Hook {hook.Id} is failClosed and {reason}." : null;
        return new HookStep(
            HookStepFactory.Record(hook, context, at, status, deny ? "deny" : "none", null, duration, message, null, null, false, payloadSha256, reason),
            null,
            deny ? failClosedCause : HookDecisionCause.None,
            message,
            message,
            true);
    }

    private static HookStep ExitCodeTwo(
        EffectiveHook hook,
        HookDispatchContext context,
        HookCommandOutcome outcome,
        TimeSpan duration,
        string sha,
        DateTimeOffset at)
    {
        var parsed = HookOutputParser.Parse(outcome.StandardOutput, context.Event);
        var fromStderr = outcome.StandardError.Trim();
        var agentMessage = FirstNonEmpty(parsed.AgentMessage, parsed.UserMessage, fromStderr) ?? $"Blocked by hook {hook.Id} (exit code 2).";
        var userMessage = FirstNonEmpty(parsed.UserMessage, parsed.AgentMessage, fromStderr) ?? agentMessage;
        return new HookStep(
            Record(hook, context, outcome, duration, sha, at, HookRunStatus.Denied, "deny", userMessage, "exit code 2"),
            null,
            HookDecisionCause.ExitCodeTwo,
            agentMessage,
            userMessage,
            true);
    }

    private static HookStep Failure(
        EffectiveHook hook,
        HookDispatchContext context,
        HookCommandOutcome outcome,
        TimeSpan duration,
        string sha,
        DateTimeOffset at,
        string reason,
        HookDecisionCause cause)
    {
        var deny = FailsClosed(hook, context);
        var message = deny ? $"Hook {hook.Id} is failClosed and failed: {reason}." : null;
        return new HookStep(
            Record(hook, context, outcome, duration, sha, at, HookRunStatus.Failed, deny ? "deny" : "none", message, reason),
            null,
            deny ? cause : HookDecisionCause.None,
            message,
            message,
            true);
    }

    private static HookStep Empty(
        EffectiveHook hook,
        HookDispatchContext context,
        HookCommandOutcome outcome,
        TimeSpan duration,
        string sha,
        DateTimeOffset at)
    {
        if (FailsClosed(hook, context))
        {
            return Failure(hook, context, outcome, duration, sha, at, "it produced no output", HookDecisionCause.FailClosedFailure);
        }

        return new HookStep(
            Record(hook, context, outcome, duration, sha, at, HookRunStatus.Succeeded, "none", null, "no output"),
            null,
            HookDecisionCause.None,
            null,
            null,
            true);
    }

    private static HookStep Invalid(
        EffectiveHook hook,
        HookDispatchContext context,
        HookCommandOutcome outcome,
        TimeSpan duration,
        string sha,
        DateTimeOffset at,
        string problem)
    {
        if (hook.Scope == HookScope.Plugin)
        {
            // Plugin manifest hooks predate the JSON protocol: any exit 0 output that is not a decision means allow.
            return new HookStep(
                Record(hook, context, outcome, duration, sha, at, HookRunStatus.Succeeded, "none", null, "plain output"),
                null,
                HookDecisionCause.None,
                null,
                null,
                true);
        }

        var deny = context.Info is { Kind: HookEventKind.Permission };
        var message = deny ? $"Hook {hook.Id} returned an invalid response: {problem}." : null;
        return new HookStep(
            Record(hook, context, outcome, duration, sha, at, HookRunStatus.InvalidResponse, deny ? "deny" : "none", message, problem),
            null,
            deny ? HookDecisionCause.InvalidResponse : HookDecisionCause.None,
            message,
            message,
            true);
    }

    private static HookStep Valid(
        EffectiveHook hook,
        HookDispatchContext context,
        HookCommandOutcome outcome,
        TimeSpan duration,
        string sha,
        DateTimeOffset at,
        HookOutputParse parsed)
    {
        var denied = parsed.Verdict == HookVerdict.Deny;
        var decision = parsed.Verdict switch
        {
            HookVerdict.Allow => "allow",
            HookVerdict.Ask => "ask",
            HookVerdict.Deny => "deny",
            _ => "none",
        };
        return new HookStep(
            Record(hook, context, outcome, duration, sha, at, denied ? HookRunStatus.Denied : HookRunStatus.Succeeded, decision, parsed.UserMessage, null),
            parsed,
            HookDecisionCause.None,
            null,
            null,
            true);
    }

    private static bool FailsClosed(EffectiveHook hook, HookDispatchContext context) => hook.FailClosed && context.CanBlock;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static HookRunRecord Record(
        EffectiveHook hook,
        HookDispatchContext context,
        HookCommandOutcome outcome,
        TimeSpan duration,
        string sha,
        DateTimeOffset at,
        HookRunStatus status,
        string decision,
        string? userMessage,
        string? note) =>
        HookStepFactory.Record(
            hook,
            context,
            at,
            status,
            decision,
            outcome.ExitCode,
            duration,
            userMessage,
            outcome.StandardOutput,
            outcome.StandardError,
            outcome.StandardOutputTruncated || outcome.StandardErrorTruncated,
            sha,
            note);
}
