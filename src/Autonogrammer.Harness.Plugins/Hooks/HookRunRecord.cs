namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The log record of one hook in one dispatch. It carries a SHA-256 of the input payload, never the payload itself: a
/// payload can hold file contents and secrets.
/// </summary>
/// <param name="At">When the hook finished (or was skipped).</param>
/// <param name="SessionId">The conversation the dispatch belongs to.</param>
/// <param name="GenerationId">The generation (user message) the dispatch belongs to, or null outside a session.</param>
/// <param name="EventName">The event that was dispatched.</param>
/// <param name="HookId">The hook's identifier.</param>
/// <param name="PluginId">The plugin or pseudo plugin the hook is grouped under.</param>
/// <param name="Scope">Where the hook comes from.</param>
/// <param name="MatchedValue">
/// The value the matcher was tested against (a tool type or subagent type, cut at 128 characters), or null. It is null for the
/// shell events, whose value is the whole command line.
/// </param>
/// <param name="Status">What happened.</param>
/// <param name="Decision">The hook's own verdict: <c>allow</c>, <c>ask</c>, <c>deny</c>, or <c>none</c>.</param>
/// <param name="ExitCode">The exit code, when the process exited.</param>
/// <param name="Duration">How long the hook ran; zero when it did not start.</param>
/// <param name="UserMessage">The message the hook asked to show the user.</param>
/// <param name="StandardOutputExcerpt">At most 4 KiB of standard output.</param>
/// <param name="StandardErrorExcerpt">At most 4 KiB of standard error.</param>
/// <param name="OutputTruncated">True when either stream was longer than the excerpt or the capture cap.</param>
/// <param name="PayloadSha256">The lowercase SHA-256 hex digest of the input JSON, or null when the hook did not start.</param>
/// <param name="Note">A short explanation: the loop limit, a parse problem, a quarantine reason.</param>
public sealed record HookRunRecord(
    DateTimeOffset At,
    string SessionId,
    string? GenerationId,
    string EventName,
    string HookId,
    string PluginId,
    HookScope Scope,
    string? MatchedValue,
    HookRunStatus Status,
    string Decision,
    int? ExitCode,
    TimeSpan Duration,
    string? UserMessage,
    string? StandardOutputExcerpt,
    string? StandardErrorExcerpt,
    bool OutputTruncated,
    string? PayloadSha256,
    string? Note);
