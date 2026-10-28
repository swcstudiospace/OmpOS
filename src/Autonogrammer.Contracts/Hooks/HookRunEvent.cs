namespace Autonogrammer.Contracts.Hooks;

/// <summary>
/// The data of a <c>hook</c> stream event: what happened to one hook in one dispatch. It carries a digest of the hook's input, never the
/// input itself, and the output excerpts are already redacted (see <see cref="HookLogRedaction"/>).
/// </summary>
public sealed record HookRunEvent
{
    /// <summary>Gets when the hook finished (or was skipped).</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>Gets the conversation the dispatch belongs to.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the generation (user message) the dispatch belongs to, or null.</summary>
    public string? GenerationId { get; init; }

    /// <summary>Gets the event that was dispatched, for example <c>beforeShellExecution</c> or the omp event name.</summary>
    public required string EventName { get; init; }

    /// <summary>Gets the hook's identifier.</summary>
    public required string HookId { get; init; }

    /// <summary>Gets where the hook comes from: <c>managed</c>, <c>project</c>, <c>user</c>, <c>claudeLocal</c>, <c>claudeProject</c>, <c>claudeUser</c> or <c>plugin</c>.</summary>
    public required string Scope { get; init; }

    /// <summary>Gets what happened: <c>succeeded</c>, <c>denied</c>, <c>failed</c>, <c>timedOut</c>, <c>skipped</c>, <c>faulted</c>, <c>awaitingConsent</c>, <c>pluginQuarantined</c> or <c>invalidResponse</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the hook's own verdict: <c>allow</c>, <c>ask</c>, <c>deny</c> or <c>none</c>.</summary>
    public required string Decision { get; init; }

    /// <summary>Gets the exit code, when the process exited.</summary>
    public int? ExitCode { get; init; }

    /// <summary>Gets how long the hook ran, in milliseconds; zero when it did not start.</summary>
    public required long DurationMs { get; init; }

    /// <summary>Gets the value the matcher was tested against, or null.</summary>
    public string? MatchedValue { get; init; }

    /// <summary>Gets the message the hook asked to show the user, redacted.</summary>
    public string? UserMessage { get; init; }

    /// <summary>Gets at most 4 KiB of the hook's standard output, redacted.</summary>
    public string? StandardOutputExcerpt { get; init; }

    /// <summary>Gets at most 4 KiB of the hook's standard error, redacted.</summary>
    public string? StandardErrorExcerpt { get; init; }

    /// <summary>Gets a value indicating whether an output stream was longer than its excerpt.</summary>
    public bool OutputTruncated { get; init; }

    /// <summary>Gets the SHA-256 digest of the hook's input JSON in lowercase hex, or null when the hook did not start.</summary>
    public string? PayloadSha256 { get; init; }

    /// <summary>Gets a short explanation: the loop limit, a parse problem or a quarantine reason.</summary>
    public string? Note { get; init; }
}
