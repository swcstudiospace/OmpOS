using Microsoft.AspNetCore.Http.HttpResults;

namespace OmpOS.Api.Services;

/// <summary>Builds the RFC 9457 problems of the web-app endpoints. Each carries a stable code in its <c>type</c> member: <c>/problems/{code}</c>.</summary>
internal static class WebProblems
{
    /// <summary>The code of a session that the operator closed.</summary>
    public const string SessionClosed = "session_closed";

    /// <summary>Builds a problem with a stable code.</summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="code">The stable machine-readable code, for example <c>steering_unavailable</c>.</param>
    /// <param name="title">The problem title.</param>
    /// <param name="detail">The problem detail. It never repeats a prompt, a message text or a credential.</param>
    /// <returns>The problem response.</returns>
    public static ProblemHttpResult Of(int statusCode, string code, string title, string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return TypedResults.Problem(detail: detail, statusCode: statusCode, title: title, type: $"/problems/{code}");
    }
}
