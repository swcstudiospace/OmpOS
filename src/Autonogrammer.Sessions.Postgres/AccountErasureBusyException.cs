namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// The erasure could not take the lifecycle lock of the account because writers that were already admitted held it past the lock timeout. Nothing was fenced or deleted,
/// and the erasure can be repeated. The message names no account.
/// </summary>
public sealed class AccountErasureBusyException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="AccountErasureBusyException"/> class.</summary>
    public AccountErasureBusyException()
        : base("Writers that were already admitted still hold the account.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AccountErasureBusyException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public AccountErasureBusyException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AccountErasureBusyException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public AccountErasureBusyException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
