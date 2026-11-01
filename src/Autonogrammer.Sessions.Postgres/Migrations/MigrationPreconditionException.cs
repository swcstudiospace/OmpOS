namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// A migration step whose precondition does not hold on this server. The message starts with a stable error code (for example
/// <c>retention_requires_timescale_license</c>) so an operator and a status report can name the cause.
/// </summary>
public sealed class MigrationPreconditionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MigrationPreconditionException"/> class.</summary>
    public MigrationPreconditionException()
        : this("unknown", "A migration precondition does not hold.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MigrationPreconditionException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public MigrationPreconditionException(string message)
        : base(message)
    {
        Code = "unknown";
    }

    /// <summary>Initializes a new instance of the <see cref="MigrationPreconditionException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public MigrationPreconditionException(string message, Exception? innerException)
        : base(message, innerException)
    {
        Code = "unknown";
    }

    /// <summary>Initializes a new instance of the <see cref="MigrationPreconditionException"/> class.</summary>
    /// <param name="code">The stable error code, for example <c>retention_requires_timescale_license</c>.</param>
    /// <param name="detail">What the operator needs to know to fix it.</param>
    public MigrationPreconditionException(string code, string detail)
        : base($"{code}: {detail}")
    {
        Code = code;
    }

    /// <summary>Gets the stable error code.</summary>
    public string Code { get; }
}
