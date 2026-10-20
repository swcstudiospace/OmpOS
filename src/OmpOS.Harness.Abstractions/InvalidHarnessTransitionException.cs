namespace OmpOS.Harness.Abstractions;

/// <summary>A requested lifecycle transition is not in the allowed table.</summary>
public sealed class InvalidHarnessTransitionException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="InvalidHarnessTransitionException"/> class.</summary>
    public InvalidHarnessTransitionException()
        : base("The harness state transition is not allowed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidHarnessTransitionException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public InvalidHarnessTransitionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidHarnessTransitionException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public InvalidHarnessTransitionException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidHarnessTransitionException"/> class.</summary>
    /// <param name="from">The state the session was in.</param>
    /// <param name="to">The state that was requested.</param>
    /// <param name="context">Optional context.</param>
    public InvalidHarnessTransitionException(HarnessState from, HarnessState to, string? context = null)
        : base($"Harness transition {from} -> {to} is not allowed.", context, null)
    {
        From = from;
        To = to;
    }

    /// <summary>Gets the state the session was in, when known.</summary>
    public HarnessState? From { get; }

    /// <summary>Gets the state that was requested, when known.</summary>
    public HarnessState? To { get; }
}
