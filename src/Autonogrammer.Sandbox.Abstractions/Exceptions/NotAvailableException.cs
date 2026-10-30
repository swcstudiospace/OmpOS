namespace Autonogrammer.Sandbox.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a provider or feature is not available.
/// </summary>
public sealed class NotAvailableException : SandboxException
{
    /// <inheritdoc />
    public override string ErrorCode => "SBOX_NOT_AVAILABLE";

    /// <summary>
    /// Gets the reason for unavailability.
    /// </summary>
    public string Reason { get; }

    /// <inheritdoc />
    public NotAvailableException(string reason)
        : base($"Not available: {reason}")
    {
        Reason = reason;
    }

    /// <inheritdoc />
    public NotAvailableException(string reason, Exception innerException)
        : base($"Not available: {reason}", innerException)
    {
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotAvailableException"/> class with a default message.
    /// </summary>
    public NotAvailableException()
        : base()
    {
        Reason = string.Empty;
    }
}
