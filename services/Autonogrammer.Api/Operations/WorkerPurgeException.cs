using System.Net;

namespace Autonogrammer.Api.Operations;

/// <summary>
/// A purge the worker refused or could not finish: any answer other than 204 or 404, including 400, 401, 403, 409 (a session of the owner is attached) and 5xx.
/// It carries the status only; neither the token nor the response body reaches it.
/// </summary>
internal sealed class WorkerPurgeException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkerPurgeException"/> class.</summary>
    /// <param name="message">The message that describes the refusal.</param>
    public WorkerPurgeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WorkerPurgeException"/> class.</summary>
    /// <param name="message">The message that describes the refusal.</param>
    /// <param name="innerException">The exception that caused the refusal.</param>
    public WorkerPurgeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WorkerPurgeException"/> class.</summary>
    /// <param name="statusCode">The status the worker answered with.</param>
    public WorkerPurgeException(HttpStatusCode statusCode)
        : base($"The worker refused the account purge with HTTP {(int)statusCode}.")
    {
        StatusCode = statusCode;
    }

    /// <summary>Gets the status the worker answered with.</summary>
    public HttpStatusCode StatusCode { get; }

    public WorkerPurgeException()
    {
    }
}
