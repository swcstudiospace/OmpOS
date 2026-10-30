namespace Autonogrammer.Sandbox.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a policy denies an operation.
/// </summary>
public sealed class PolicyDeniedException : SandboxException
{
    /// <inheritdoc />
    public override string ErrorCode => "SBOX_POLICY_DENIED";

    /// <summary>
    /// Gets the policy that denied the operation.
    /// </summary>
    public string Policy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyDeniedException"/> class for a denied policy.
    /// </summary>
    /// <param name="policy">The policy that denied the operation.</param>
    /// <param name="message">The error message.</param>
    public PolicyDeniedException(string policy, string message)
        : base(message)
    {
        Policy = policy;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyDeniedException"/> class for a denied policy with an inner exception.
    /// </summary>
    /// <param name="policy">The policy that denied the operation.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PolicyDeniedException(string policy, string message, Exception innerException)
        : base(message, innerException)
    {
        Policy = policy;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyDeniedException"/> class with an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PolicyDeniedException(string message)
        : base(message)
    {
        Policy = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyDeniedException"/> class with an error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PolicyDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
        Policy = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyDeniedException"/> class with a default message.
    /// </summary>
    public PolicyDeniedException()
        : base()
    {
        Policy = string.Empty;
    }
}
