namespace OmpOS.Harness.Abstractions;

/// <summary>The engine handshake did not complete in time.</summary>
public sealed class HandshakeTimeoutHarnessException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="HandshakeTimeoutHarnessException"/> class.</summary>
    public HandshakeTimeoutHarnessException()
        : base("The engine handshake timed out.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HandshakeTimeoutHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public HandshakeTimeoutHarnessException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HandshakeTimeoutHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public HandshakeTimeoutHarnessException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HandshakeTimeoutHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="context">Optional context.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    public HandshakeTimeoutHarnessException(string message, string? context = null, Exception? innerException = null)
        : base(message, context, innerException)
    {
    }
}
