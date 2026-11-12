namespace Autonogrammer.Sessions.Postgres;

/// <summary>Thrown when a writer is refused because the account is being erased or has been erased.</summary>
public sealed class AccountErasingException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AccountErasingException"/> class.</summary>
    public AccountErasingException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AccountErasingException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public AccountErasingException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AccountErasingException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public AccountErasingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
