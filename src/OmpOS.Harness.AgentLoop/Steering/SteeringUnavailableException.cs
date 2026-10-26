namespace OmpOS.Harness.AgentLoop.Steering;

/// <summary>
/// A directive could not be put into the steering inbox. <see cref="Exception.Message"/> is plain text that is safe to show to
/// the user; it never contains the text of the directive or a file path.
/// </summary>
public sealed class SteeringUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SteeringUnavailableException"/> class.</summary>
    public SteeringUnavailableException()
        : this("The session cannot be steered right now.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SteeringUnavailableException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public SteeringUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SteeringUnavailableException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public SteeringUnavailableException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SteeringUnavailableException"/> class.</summary>
    /// <param name="reason">Why the directive was not queued.</param>
    /// <param name="message">The failure message, safe to show.</param>
    public SteeringUnavailableException(SteeringFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>Initializes a new instance of the <see cref="SteeringUnavailableException"/> class.</summary>
    /// <param name="reason">Why the directive was not queued.</param>
    /// <param name="message">The failure message, safe to show.</param>
    /// <param name="innerException">The underlying exception.</param>
    public SteeringUnavailableException(SteeringFailureReason reason, string message, Exception? innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    /// <summary>Gets why the directive was not queued.</summary>
    public SteeringFailureReason Reason { get; }
}
