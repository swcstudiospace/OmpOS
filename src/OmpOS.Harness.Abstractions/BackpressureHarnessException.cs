namespace OmpOS.Harness.Abstractions;

/// <summary>A bounded event buffer overflowed because the consumer fell behind.</summary>
public sealed class BackpressureHarnessException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="BackpressureHarnessException"/> class.</summary>
    public BackpressureHarnessException()
        : base("The event buffer overflowed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BackpressureHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public BackpressureHarnessException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BackpressureHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public BackpressureHarnessException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BackpressureHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="context">Optional context.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    public BackpressureHarnessException(string message, string? context = null, Exception? innerException = null)
        : base(message, context, innerException)
    {
    }
}
