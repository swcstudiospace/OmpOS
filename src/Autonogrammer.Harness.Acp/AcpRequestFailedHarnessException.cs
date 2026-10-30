using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Acp;

/// <summary>An ACP request was answered with a JSON-RPC error, did not complete in time, or was answered with an unusable result.</summary>
public sealed class AcpRequestFailedHarnessException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="AcpRequestFailedHarnessException"/> class.</summary>
    public AcpRequestFailedHarnessException()
        : base("An ACP request failed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AcpRequestFailedHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public AcpRequestFailedHarnessException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AcpRequestFailedHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public AcpRequestFailedHarnessException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AcpRequestFailedHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="context">Optional context, typically the ACP method name.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    public AcpRequestFailedHarnessException(string message, string? context = null, Exception? innerException = null)
        : base(message, context, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AcpRequestFailedHarnessException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="context">The ACP method name.</param>
    /// <param name="errorCode">The JSON-RPC error code, when the agent replied with an error.</param>
    /// <param name="timedOut">Whether the request timed out.</param>
    public AcpRequestFailedHarnessException(string message, string context, long? errorCode, bool timedOut)
        : base(message, context, null)
    {
        ErrorCode = errorCode;
        TimedOut = timedOut;
    }

    /// <summary>Gets the JSON-RPC error code, when the agent replied with an error.</summary>
    public long? ErrorCode { get; }

    /// <summary>Gets a value indicating whether the request timed out before the agent replied.</summary>
    public bool TimedOut { get; }
}
