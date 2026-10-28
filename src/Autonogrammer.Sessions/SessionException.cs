using Autonogrammer.Contracts.Errors;

namespace Autonogrammer.Sessions;

/// <summary>
/// Carries a typed <see cref="AutonogrammerError"/> across the session service boundary.
/// <see cref="AutonogrammerError"/> is a value record, so the service throws this wrapper to signal rejection.
/// </summary>
public sealed class SessionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SessionException"/> class.</summary>
    public SessionException()
        : this(new AutonogrammerError { Code = ErrorCode.Unknown, Detail = "A session error occurred." })
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SessionException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public SessionException(string message)
        : this(new AutonogrammerError { Code = ErrorCode.Unknown, Detail = message })
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SessionException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public SessionException(string message, Exception? innerException)
        : base(message, innerException)
    {
        Error = new AutonogrammerError { Code = ErrorCode.Unknown, Detail = message };
    }

    /// <summary>Initializes a new instance of the <see cref="SessionException"/> class.</summary>
    /// <param name="error">The typed error this exception carries.</param>
    public SessionException(AutonogrammerError error)
        : base(DetailOf(error))
    {
        Error = error;
    }

    /// <summary>Gets the typed error (machine-readable code plus detail).</summary>
    public AutonogrammerError Error { get; }

    private static string DetailOf(AutonogrammerError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Detail;
    }
}
