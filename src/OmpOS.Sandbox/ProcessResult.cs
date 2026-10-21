namespace OmpOS.Sandbox;

/// <summary>
/// Result of a process execution.
/// </summary>
public sealed record ProcessResult
{
    /// <summary>
    /// Gets the exit code.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Gets the standard output.
    /// </summary>
    public required string Stdout { get; init; }

    /// <summary>
    /// Gets the standard error.
    /// </summary>
    public required string Stderr { get; init; }

    /// <summary>
    /// Gets whether captured output was truncated at the per-stream cap.
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// Gets whether the process timed out.
    /// </summary>
    public required bool TimedOut { get; init; }
}
