using Microsoft.AspNetCore.RateLimiting;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Shared 429 rejection for the API's global rate-limit policies: sets the status,
/// hints <c>Retry-After: 60</c>, and logs the method and path without prompt content.
/// </summary>
internal static class RateLimitRejection
{
    private static readonly Action<ILogger, string, string, Exception?> Rejected =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1, "RateLimitRejected"),
            "Rate limit rejected {Method} {Path}");

    /// <summary>Builds the rejection handler logging through <paramref name="logger"/>.</summary>
    /// <param name="logger">The logger recording the rejection.</param>
    /// <returns>The rate-limiter rejection callback.</returns>
    public static Func<OnRejectedContext, CancellationToken, ValueTask> Handler(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return (context, cancellationToken) =>
        {
            ArgumentNullException.ThrowIfNull(context);
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.Headers.RetryAfter = "60";
            Rejected(
                logger,
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path.ToString(),
                null);
            return ValueTask.CompletedTask;
        };
    }
}
