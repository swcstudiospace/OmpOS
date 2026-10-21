namespace OmpOS.Harness.Abstractions;

/// <summary>
/// Base type of every typed harness failure. Carries an optional context string
/// (for example the session id or script step) and an optional inner exception.
/// </summary>
public abstract class HarnessException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="HarnessException"/> class.</summary>
    protected HarnessException()
        : this("A harness failure occurred.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    protected HarnessException(string message)
        : this(message, null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    protected HarnessException(string message, Exception? innerException)
        : this(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="context">Optional context describing where the failure occurred.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    protected HarnessException(string message, string? context, Exception? innerException)
        : base(message, innerException)
    {
        Context = context;
    }

    /// <summary>Gets the optional context describing where the failure occurred.</summary>
    public string? Context { get; }
}
