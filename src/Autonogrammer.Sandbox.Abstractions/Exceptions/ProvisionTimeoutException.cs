namespace Autonogrammer.Sandbox.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when provisioning times out.
/// </summary>
public sealed class ProvisionTimeoutException : SandboxException
{
    /// <inheritdoc />
    public override string ErrorCode => "SBOX_PROVISION_TIMEOUT";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProvisionTimeoutException"/> class with an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ProvisionTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProvisionTimeoutException"/> class with an error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ProvisionTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProvisionTimeoutException"/> class with a default message.
    /// </summary>
    public ProvisionTimeoutException()
        : base()
    {
    }
}
