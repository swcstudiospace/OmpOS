namespace Autonogrammer.Resilience;

/// <summary>
/// A write that the account erasure fence refused: the account that owns the row is being erased or has been erased, so nothing may be written for it. The
/// fence is in the database, so every writer sees it. It is never retried and never counts against the breaker, because the database answered. It carries the
/// SQLSTATE of the refusal; its message names no account.
/// </summary>
public sealed class AccountErasingException : Exception
{
    /// <summary>The message of an exception built without one.</summary>
    public const string DefaultMessage = "The account is being erased or has been erased; nothing may be written for it.";

    /// <summary>Initializes a new instance of the <see cref="AccountErasingException"/> class.</summary>
    public AccountErasingException()
        : this(DefaultMessage)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AccountErasingException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public AccountErasingException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AccountErasingException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public AccountErasingException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets the SQLSTATE of the refusal, or an empty string when the exception was not built from one.</summary>
    public string SqlState { get; init; } = string.Empty;
}
