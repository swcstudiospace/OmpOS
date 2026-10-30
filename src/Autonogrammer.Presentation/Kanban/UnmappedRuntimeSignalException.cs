namespace Autonogrammer.Presentation.Kanban;

/// <summary>
/// Thrown when a runtime signal kind has no entry in <see cref="RuntimeStatusMap"/>. The card keeps its previous status; no default
/// status is applied.
/// </summary>
public sealed class UnmappedRuntimeSignalException : ArgumentException
{
    /// <summary>Creates an exception with no signal details.</summary>
    public UnmappedRuntimeSignalException()
    {
    }

    /// <summary>Creates an exception with a message and no signal details.</summary>
    /// <param name="message">The message.</param>
    public UnmappedRuntimeSignalException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and an inner exception, and no signal details.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public UnmappedRuntimeSignalException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a signal kind that has no status mapping. The parameter name is <c>kind</c>.</summary>
    /// <param name="kind">The signal kind that has no status mapping.</param>
    public UnmappedRuntimeSignalException(RuntimeSignalKind kind)
        : this(kind, nameof(kind))
    {
    }

    /// <summary>Creates the exception for a signal kind that has no status mapping, naming the parameter that carried it.</summary>
    /// <param name="kind">The signal kind that has no status mapping.</param>
    /// <param name="paramName">The name of the parameter that carried the kind.</param>
    public UnmappedRuntimeSignalException(RuntimeSignalKind kind, string paramName)
        : base($"Runtime signal kind {kind} has no status mapping.", paramName)
    {
        Kind = kind;
    }

    /// <summary>Gets the signal kind that has no status mapping, or null when the exception carries no signal details.</summary>
    public RuntimeSignalKind? Kind { get; }
}
