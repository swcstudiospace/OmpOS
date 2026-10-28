namespace Autonogrammer.Sandbox.Abstractions.Exceptions;

/// <summary>
/// Base exception for sandbox operations.
/// </summary>
public abstract class SandboxException : Exception
{
    /// <summary>
    /// Gets the Autonogrammer error code.
    /// </summary>
    public abstract string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxException"/> class with an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected SandboxException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxException"/> class with an error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    protected SandboxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxException"/> class with a default message.
    /// </summary>
    protected SandboxException()
        : base()
    {
    }
}
