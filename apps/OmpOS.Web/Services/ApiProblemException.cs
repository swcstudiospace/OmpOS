using System.Net;

namespace OmpOS.Web.Services;

/// <summary>
/// A failed API call that answered with a problem response. It carries the status, the problem code (the last segment of the problem
/// <c>type</c>, for example <c>steering_unavailable</c>), the title and a bounded detail. The message never contains a request body, a prompt
/// or a credential.
/// </summary>
internal sealed class ApiProblemException : ApiClientException
{
    /// <summary>The longest detail kept, in characters.</summary>
    internal const int MaxDetailChars = 300;

    /// <summary>Initializes a new instance of the <see cref="ApiProblemException"/> class.</summary>
    public ApiProblemException()
        : this(ApiProblemKind.Other, HttpStatusCode.InternalServerError, null, "API request failed.", null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiProblemException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public ApiProblemException(string message)
        : this(ApiProblemKind.Other, HttpStatusCode.InternalServerError, null, message, null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiProblemException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The cause.</param>
    public ApiProblemException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = ApiProblemKind.Other;
    }

    /// <summary>Initializes a new instance of the <see cref="ApiProblemException"/> class.</summary>
    /// <param name="kind">The class of failure.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="code">The problem code, or null when the response named none.</param>
    /// <param name="title">The problem title.</param>
    /// <param name="detail">The problem detail, or null.</param>
    /// <param name="retryAfter">How long the API asked the caller to wait, or null.</param>
    internal ApiProblemException(
        ApiProblemKind kind,
        HttpStatusCode statusCode,
        string? code,
        string title,
        string? detail,
        TimeSpan? retryAfter)
        : base(statusCode, Describe(statusCode, code, title), null)
    {
        Kind = kind;
        Code = code;
        Title = title;
        Detail = detail is null || detail.Length <= MaxDetailChars ? detail : detail[..MaxDetailChars];
        RetryAfter = retryAfter;
    }

    /// <summary>Gets the class of failure.</summary>
    internal ApiProblemKind Kind { get; }

    /// <summary>Gets the problem code (for example <c>session_closed</c>), or null.</summary>
    internal string? Code { get; }

    /// <summary>Gets the problem title.</summary>
    internal string Title { get; } = string.Empty;

    /// <summary>Gets the problem detail, shortened to <see cref="MaxDetailChars"/>, or null.</summary>
    internal string? Detail { get; }

    /// <summary>Gets how long a rate-limited caller was asked to wait, or null.</summary>
    internal TimeSpan? RetryAfter { get; }

    /// <summary>Maps an HTTP status to a kind.</summary>
    /// <param name="statusCode">The status.</param>
    /// <returns>The kind.</returns>
    internal static ApiProblemKind KindOf(HttpStatusCode statusCode) => (int)statusCode switch
    {
        400 => ApiProblemKind.BadRequest,
        401 => ApiProblemKind.Unauthorized,
        403 => ApiProblemKind.Forbidden,
        404 => ApiProblemKind.NotFound,
        409 => ApiProblemKind.Conflict,
        413 => ApiProblemKind.PayloadTooLarge,
        415 => ApiProblemKind.UnsupportedMediaType,
        422 => ApiProblemKind.Unprocessable,
        429 => ApiProblemKind.RateLimited,
        >= 500 => ApiProblemKind.Server,
        _ => ApiProblemKind.Other,
    };

    private static string Describe(HttpStatusCode statusCode, string? code, string title) =>
        code is null
            ? $"API request failed with {(int)statusCode}: {title}"
            : $"API request failed with {(int)statusCode} ({code}): {title}";
}
