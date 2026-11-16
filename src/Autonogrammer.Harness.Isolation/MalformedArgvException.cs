namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// A bubblewrap argument is incomplete, unknown, out of precedence, or a grant cannot be expressed as a complete bind.
/// The session fails closed on it; it is never emitted as a partial argument.
/// </summary>
public sealed class MalformedArgvException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="MalformedArgvException"/> class.</summary>
    public MalformedArgvException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MalformedArgvException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public MalformedArgvException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MalformedArgvException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public MalformedArgvException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
