namespace Autonogrammer.Presentation.Cloud;

/// <summary>
/// An expected failure of an <see cref="ICloudAgentsHost"/> call. The message is shown to the user, so a host must make it plain and must
/// never put a token, a secret value or a worker address in it.
/// </summary>
public sealed class CloudHostException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CloudHostException"/> class with <see cref="CloudFailureKind.Unavailable"/>.</summary>
    public CloudHostException()
    {
        Kind = CloudFailureKind.Unavailable;
    }

    /// <summary>Initializes a new instance of the <see cref="CloudHostException"/> class with <see cref="CloudFailureKind.Unavailable"/>.</summary>
    /// <param name="message">The message shown to the user.</param>
    public CloudHostException(string message)
        : base(message)
    {
        Kind = CloudFailureKind.Unavailable;
    }

    /// <summary>Initializes a new instance of the <see cref="CloudHostException"/> class with <see cref="CloudFailureKind.Unavailable"/>.</summary>
    /// <param name="message">The message shown to the user.</param>
    /// <param name="innerException">The cause.</param>
    public CloudHostException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = CloudFailureKind.Unavailable;
    }

    /// <summary>Initializes a new instance of the <see cref="CloudHostException"/> class.</summary>
    /// <param name="kind">Why the call failed.</param>
    /// <param name="message">The message shown to the user.</param>
    public CloudHostException(CloudFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>Initializes a new instance of the <see cref="CloudHostException"/> class.</summary>
    /// <param name="kind">Why the call failed.</param>
    /// <param name="message">The message shown to the user.</param>
    /// <param name="innerException">The cause.</param>
    public CloudHostException(CloudFailureKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>Gets why the call failed.</summary>
    public CloudFailureKind Kind { get; }
}
