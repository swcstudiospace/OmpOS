namespace OmpOS.Web.Services;

/// <summary>
/// A failed API call: the HTTP status code plus the response body, if any.
/// </summary>
internal sealed class ApiClientException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ApiClientException"/> class.</summary>
    public ApiClientException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiClientException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public ApiClientException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiClientException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The cause.</param>
    public ApiClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiClientException"/> class.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body, if any.</param>
    internal ApiClientException(System.Net.HttpStatusCode statusCode, string? body)
        : base($"API request failed with {(int)statusCode} {statusCode}{(string.IsNullOrEmpty(body) ? "." : $": {body}")}")
    {
        StatusCode = statusCode;
        Body = body;
    }

    /// <summary>Gets the HTTP status code.</summary>
    internal System.Net.HttpStatusCode StatusCode { get; }

    /// <summary>Gets the response body, if any.</summary>
    internal string? Body { get; }
}
