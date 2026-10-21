namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Execution event.
/// </summary>
public sealed record ExecEvent
{
    /// <summary>Gets the event type.</summary>
    public required ExecEventType Type { get; init; }

    /// <summary>Gets the stdout data (for Stdout events).</summary>
    public string? Stdout { get; init; }

    /// <summary>Gets the stderr data (for Stderr events).</summary>
    public string? Stderr { get; init; }

    /// <summary>Gets the exit code (for Exit events).</summary>
    public int? ExitCode { get; init; }
}
