using Autonogrammer.Contracts.Cowork;

namespace Autonogrammer.Cowork;

/// <summary>The Cowork registry already holds the most tasks, so another cannot be started.</summary>
public sealed class CoworkSessionLimitException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="CoworkSessionLimitException"/> class with <see cref="CoworkSessionLimit.FullMessage"/>.</summary>
    public CoworkSessionLimitException()
        : base(CoworkSessionLimit.FullMessage)
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

    /// <summary>Initializes a new instance of the <see cref="CoworkSessionLimitException"/> class with <see cref="CoworkSessionLimit.FullMessage"/>.</summary>
    /// <param name="innerException">The registry failure that was translated.</param>
    public CoworkSessionLimitException(Exception innerException)
        : this(CoworkSessionLimit.FullMessage, innerException)
    {
    }
}
