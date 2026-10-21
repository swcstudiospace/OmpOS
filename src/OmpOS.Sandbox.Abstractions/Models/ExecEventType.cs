namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Execution event type.
/// </summary>
public enum ExecEventType
{
    /// <summary>Standard output.</summary>
    Stdout,

    /// <summary>Standard error.</summary>
    Stderr,

    /// <summary>Process exited.</summary>
    Exit,
}
