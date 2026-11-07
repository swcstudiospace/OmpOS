namespace Autonogrammer.Cowork;

/// <summary>The Cowork registry already holds 200 tasks, so another cannot be started.</summary>
public sealed class CoworkSessionLimitException : InvalidOperationException
{
    /// <summary>The message a full Cowork registry shows.</summary>
    public const string FullMessage = "You have 200 Cowork tasks. Delete or archive one to start another.";

    /// <summary>Initializes a new instance of the <see cref="CoworkSessionLimitException"/> class with <see cref="FullMessage"/>.</summary>
    public CoworkSessionLimitException()
        : base(FullMessage)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CoworkSessionLimitException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public CoworkSessionLimitException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CoworkSessionLimitException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public CoworkSessionLimitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CoworkSessionLimitException"/> class with <see cref="FullMessage"/>.</summary>
    /// <param name="innerException">The registry failure that was translated.</param>
    public CoworkSessionLimitException(Exception innerException)
        : this(FullMessage, innerException)
    {
    }
}
