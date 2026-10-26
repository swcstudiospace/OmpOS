using System.Globalization;

namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>
/// A request to the Substrate server did not produce a usable answer. The exception carries the operation, a typed reason and
/// the HTTP status when there was one. It never carries a bearer token or any response body text.
/// </summary>
public sealed class SubstrateUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SubstrateUnavailableException"/> class.</summary>
    public SubstrateUnavailableException()
        : this("The Substrate server is unavailable.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SubstrateUnavailableException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public SubstrateUnavailableException(string message)
        : base(message)
    {
        Operation = string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="SubstrateUnavailableException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public SubstrateUnavailableException(string message, Exception? innerException)
        : base(message, innerException)
    {
        Operation = string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="SubstrateUnavailableException"/> class.</summary>
    /// <param name="operation">The request that failed, for example <c>POST /brief</c>.</param>
    /// <param name="reason">Why it failed.</param>
    /// <param name="statusCode">The HTTP status code, or null when no status was received.</param>
    public SubstrateUnavailableException(string operation, SubstrateFailureReason reason, int? statusCode)
        : base(Describe(operation, reason, statusCode))
    {
        Operation = operation;
        Reason = reason;
        StatusCode = statusCode;
    }

    /// <summary>Gets the request that failed.</summary>
    public string Operation { get; }

    /// <summary>Gets why the request failed.</summary>
    public SubstrateFailureReason Reason { get; }

    /// <summary>Gets the HTTP status code, or null when no status was received.</summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Gets a value that is true when the server is simply not answering: a broken connection, a time-out, a 5xx or a 429. A
    /// rejected request (4xx), a redirect, a foreign origin and a malformed answer are configuration or contract faults.
    /// </summary>
    public bool IsReachabilityFailure =>
        Reason is SubstrateFailureReason.Transport or SubstrateFailureReason.Timeout
        || (Reason == SubstrateFailureReason.HttpStatus && StatusCode is >= 500 or 429);

    private static string Describe(string operation, SubstrateFailureReason reason, int? statusCode)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"Substrate request {operation} failed: {reason}");
        return statusCode is { } code ? string.Create(CultureInfo.InvariantCulture, $"{text} (HTTP {code})") : text;
    }
}
