namespace OmpOS.Presentation.Hooks;

/// <summary>One hook run as the source reports it. The page redacts the text fields before it shows them.</summary>
public sealed record HookRunItem
{
    /// <summary>Gets when the hook finished or was skipped.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>Gets the conversation the run belongs to.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the event that was dispatched.</summary>
    public required string EventName { get; init; }

    /// <summary>Gets the identifier of the hook.</summary>
    public required string HookId { get; init; }

    /// <summary>Gets the name shown for the hook.</summary>
    public required string HookName { get; init; }

    /// <summary>Gets where the hook comes from.</summary>
    public required HookScopeKind Scope { get; init; }

    /// <summary>Gets what happened.</summary>
    public required HookRunOutcome Outcome { get; init; }

    /// <summary>Gets the hook's own verdict: <c>allow</c>, <c>ask</c>, <c>deny</c> or <c>none</c>.</summary>
    public string Decision { get; init; } = "none";

    /// <summary>Gets the exit code, when the process exited.</summary>
    public int? ExitCode { get; init; }

    /// <summary>Gets how long the hook ran, in milliseconds.</summary>
    public long DurationMs { get; init; }

    /// <summary>Gets the value the matcher was tested against, or null.</summary>
    public string? MatchedValue { get; init; }

    /// <summary>Gets the message the hook asked to show you, or null.</summary>
    public string? UserMessage { get; init; }

    /// <summary>Gets at most 4 KiB of the hook's standard output, or null.</summary>
    public string? StandardOutputExcerpt { get; init; }

    /// <summary>Gets at most 4 KiB of the hook's standard error, or null.</summary>
    public string? StandardErrorExcerpt { get; init; }

    /// <summary>Gets a value indicating whether an output stream was longer than its excerpt.</summary>
    public bool OutputTruncated { get; init; }

    /// <summary>Gets a short explanation, or null.</summary>
    public string? Note { get; init; }
}
