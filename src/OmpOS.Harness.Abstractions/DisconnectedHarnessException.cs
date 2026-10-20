namespace OmpOS.Harness.Abstractions;

/// <summary>The transport or engine disconnected, or the stream ended prematurely.</summary>
public sealed class DisconnectedHarnessException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="DisconnectedHarnessException"/> class.</summary>
    public DisconnectedHarnessException()
        : base("The harness disconnected.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DisconnectedHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public DisconnectedHarnessException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DisconnectedHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public DisconnectedHarnessException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DisconnectedHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="context">Optional context.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    public DisconnectedHarnessException(string message, string? context = null, Exception? innerException = null)
        : base(message, context, innerException)
    {
    }
}
