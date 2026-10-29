using Microsoft.Net.Http.Headers;

namespace Autonogrammer.Api.Hosting;

/// <summary>
/// Adds the response security headers. The content-security policy is withheld from HTML, so the Development API reference UI
/// keeps working; <c>Cache-Control: no-store</c> is added to authenticated responses that choose no caching of their own.
/// </summary>
internal sealed class SecurityHeadersMiddleware
{
    private const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    private const string PermissionsPolicy = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="SecurityHeadersMiddleware"/> class.</summary>
    /// <param name="next">The next pipeline step.</param>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Registers the header writer and invokes the pipeline.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the operation.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.OnStarting(static state =>
        {
            Apply((HttpContext)state);
            return Task.CompletedTask;
        }, context);
        return _next(context);
    }

    private static void Apply(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers[HeaderNames.XContentTypeOptions] = "nosniff";
        headers[HeaderNames.XFrameOptions] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = PermissionsPolicy;

        var contentType = context.Response.ContentType;
        if (contentType is null || !contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            headers[HeaderNames.ContentSecurityPolicy] = ContentSecurityPolicy;
        }

        var authenticated = context.User.Identity?.IsAuthenticated == true || context.Request.Headers.ContainsKey(HeaderNames.Authorization);
        if (authenticated && !headers.ContainsKey(HeaderNames.CacheControl))
        {
            headers[HeaderNames.CacheControl] = "no-store";
        }
    }
}
