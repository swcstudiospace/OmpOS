namespace Autonogrammer.Contracts.Intents;

/// <summary>
/// A parser diagnostic attached to an intent envelope.
/// </summary>
public sealed record Diagnostic
{
    /// <summary>Gets the machine-readable diagnostic code.</summary>
    public required string Code { get; init; }

    /// <summary>Gets the human-readable diagnostic message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the severity (for example "info", "warning", "error").</summary>
    public required string Severity { get; init; }
}
