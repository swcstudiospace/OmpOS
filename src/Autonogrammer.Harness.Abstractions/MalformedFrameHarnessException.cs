namespace Autonogrammer.Harness.Abstractions;

/// <summary>A frame received from the engine could not be decoded.</summary>
public sealed class MalformedFrameHarnessException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="MalformedFrameHarnessException"/> class.</summary>
    public MalformedFrameHarnessException()
        : base("A frame from the engine is malformed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MalformedFrameHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public MalformedFrameHarnessException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MalformedFrameHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public MalformedFrameHarnessException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MalformedFrameHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="context">Optional context.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    public MalformedFrameHarnessException(string message, string? context = null, Exception? innerException = null)
        : base(message, context, innerException)
    {
    }
}
