using OmpOS.Contracts.Hooks;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// Projects the harness's <see cref="HookRunRecord"/> to the contract's <see cref="HookRunEvent"/> and the <c>hook</c> stream payload. The
/// output excerpts and the user message pass through <see cref="HookLogRedaction"/> first: a record leaves the process only in this form.
/// </summary>
public static class HookRunRecordProjector
{
    /// <summary>Projects a record.</summary>
    /// <param name="record">The record.</param>
    /// <returns>The contract event.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public static HookRunEvent Project(HookRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new HookRunEvent
        {
            At = record.At,
            SessionId = record.SessionId,
            GenerationId = record.GenerationId,
            EventName = record.EventName,
            HookId = record.HookId,
            Scope = ScopeName(record.Scope),
            Status = StatusName(record.Status),
            Decision = record.Decision,
            ExitCode = record.ExitCode,
            DurationMs = (long)record.Duration.TotalMilliseconds,
            MatchedValue = record.MatchedValue,
            UserMessage = HookLogRedaction.Redact(record.UserMessage),
            StandardOutputExcerpt = HookLogRedaction.Redact(record.StandardOutputExcerpt),
            StandardErrorExcerpt = HookLogRedaction.Redact(record.StandardErrorExcerpt),
            OutputTruncated = record.OutputTruncated,
            PayloadSha256 = record.PayloadSha256,
            Note = record.Note,
        };
    }

    /// <summary>Projects a record to the payload of a <c>hook</c> stream event.</summary>
    /// <param name="record">The record.</param>
    /// <returns>The payload, an <see cref="UnknownPayload"/> with kind <c>hook</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public static UnknownPayload ToPayload(HookRunRecord record) => HookStreamPayload.Create(Project(record));

    private static string ScopeName(HookScope scope) => scope switch
    {
        HookScope.Managed => "managed",
        HookScope.Project => "project",
        HookScope.User => "user",
        HookScope.ClaudeLocal => "claudeLocal",
        HookScope.ClaudeProject => "claudeProject",
        HookScope.ClaudeUser => "claudeUser",
        HookScope.Plugin => "plugin",
        _ => scope.ToString(),
    };

    private static string StatusName(HookRunStatus status) => status switch
    {
        HookRunStatus.Succeeded => "succeeded",
        HookRunStatus.Denied => "denied",
        HookRunStatus.Failed => "failed",
        HookRunStatus.TimedOut => "timedOut",
        HookRunStatus.Skipped => "skipped",
        HookRunStatus.Faulted => "faulted",
        HookRunStatus.AwaitingConsent => "awaitingConsent",
        HookRunStatus.PluginQuarantined => "pluginQuarantined",
        HookRunStatus.InvalidResponse => "invalidResponse",
        _ => status.ToString(),
    };
}
