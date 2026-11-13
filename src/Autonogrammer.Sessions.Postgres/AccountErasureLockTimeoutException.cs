namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// Thrown when account erasure cannot take the account's lifecycle lock in time because in-flight writers still hold it. Nothing was changed; the erasure can be repeated.
/// </summary>
public sealed class AccountErasureLockTimeoutException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AccountErasureLockTimeoutException"/> class.</summary>
    public AccountErasureLockTimeoutException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AccountErasureLockTimeoutException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public AccountErasureLockTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AccountErasureLockTimeoutException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public AccountErasureLockTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
