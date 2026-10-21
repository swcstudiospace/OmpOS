namespace OmpOS.Sandbox.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when teardown fails.
/// </summary>
public sealed class TeardownFailedException : SandboxException
{
    /// <inheritdoc />
    public override string ErrorCode => "SBOX_TEARDOWN_FAILED";

    /// <summary>
    /// Gets the sandbox identifier.
    /// </summary>
    public string SandboxId { get; }

    /// <summary>
    /// Gets the number of attempts made.
    /// </summary>
    public int Attempts { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TeardownFailedException"/> class with teardown attempt details.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="attempts">The number of attempts made.</param>
    /// <param name="message">The error message.</param>
    public TeardownFailedException(string sandboxId, int attempts, string message)
        : base(message)
    {
        SandboxId = sandboxId;
        Attempts = attempts;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TeardownFailedException"/> class with teardown attempt details and an inner exception.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="attempts">The number of attempts made.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TeardownFailedException(string sandboxId, int attempts, string message, Exception innerException)
        : base(message, innerException)
    {
        SandboxId = sandboxId;
        Attempts = attempts;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TeardownFailedException"/> class with an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public TeardownFailedException(string message)
        : base(message)
    {
        SandboxId = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TeardownFailedException"/> class with an error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TeardownFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
        SandboxId = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TeardownFailedException"/> class with a default message.
    /// </summary>
    public TeardownFailedException()
        : base()
    {
        SandboxId = string.Empty;
    }
}
