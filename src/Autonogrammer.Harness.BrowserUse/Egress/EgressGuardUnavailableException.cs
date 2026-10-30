namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>The egress guard is down. Callers fail closed instead of dialing directly.</summary>
public sealed class EgressGuardUnavailableException : InvalidOperationException
{
    /// <summary>The stable reason token.</summary>
    public const string Token = "EgressGuardUnavailable";

    /// <summary>Initializes a new instance of the <see cref="EgressGuardUnavailableException"/> class.</summary>
    public EgressGuardUnavailableException()
        : base(Token)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EgressGuardUnavailableException"/> class.</summary>
    /// <param name="message">The message.</param>
    public EgressGuardUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EgressGuardUnavailableException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public EgressGuardUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
