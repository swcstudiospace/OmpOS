namespace OmpOS.Harness.AgentLoop.Sessions;

/// <summary>The registry already holds the maximum number of sessions, so another cannot be created.</summary>
public sealed class AgentSessionLimitException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="AgentSessionLimitException"/> class.</summary>
    public AgentSessionLimitException()
        : this("The session registry is full.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AgentSessionLimitException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public AgentSessionLimitException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AgentSessionLimitException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public AgentSessionLimitException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AgentSessionLimitException"/> class for a registry limit.</summary>
    /// <param name="limit">The maximum number of sessions.</param>
    public AgentSessionLimitException(int limit)
        : this($"The session registry holds at most {limit} sessions. Close a session before creating another.")
    {
        Limit = limit;
    }

    /// <summary>Gets the maximum number of sessions, or zero when the exception was not created for a limit.</summary>
    public int Limit { get; }
}
