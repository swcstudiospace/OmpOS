namespace Autonogrammer.Harness.Abstractions;

/// <summary>The engine process or transport could not be started.</summary>
public sealed class ProcessStartHarnessException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="ProcessStartHarnessException"/> class.</summary>
    public ProcessStartHarnessException()
        : base("The engine process could not be started.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ProcessStartHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public ProcessStartHarnessException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ProcessStartHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public ProcessStartHarnessException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ProcessStartHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="context">Optional context.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    public ProcessStartHarnessException(string message, string? context = null, Exception? innerException = null)
        : base(message, context, innerException)
    {
    }
}
