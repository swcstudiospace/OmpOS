using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Hosting;

/// <summary>Pipeline registration for the hosting middleware. Call it first in the pipeline.</summary>
internal static class HostingApplicationExtensions
{
    /// <summary>
    /// Adds forwarded-header handling (unless the mode is <see cref="ForwardedHeadersMode.None"/>), HSTS (outside Development) and the
    /// security-header middleware. Request timeouts are a separate step, <see cref="UseHostingRequestTimeouts"/>, because they must come after the exception handler.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseHostingHardening(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var options = app.ApplicationServices.GetRequiredService<IOptions<HostingOptions>>().Value;
        var environment = app.ApplicationServices.GetRequiredService<IHostEnvironment>();

        if (options.ForwardedHeaders.Mode != ForwardedHeadersMode.None)
        {
            _ = app.UseForwardedHeaders();
        }
        else if (!environment.IsDevelopment())
        {
            HostingLog.ForwardedHeadersIgnored(
                app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(HostingApplicationExtensions).FullName!),
                "Hosting:ForwardedHeaders:Mode");
        }

        if (!environment.IsDevelopment() && options.Hsts.Enabled)
        {
            _ = app.UseHsts();
        }

        if (options.SecurityHeaders.Enabled)
        {
            _ = app.UseMiddleware<SecurityHeadersMiddleware>();
        }

        return app;
    }

    /// <summary>
    /// Adds request timeouts. Put it after <c>UseExceptionHandler</c>: the exception handler treats an <see cref="OperationCanceledException"/> after the
    /// request token was cancelled as a client that went away and answers 499, so a handler placed inside the timeout middleware would hide the timeout
    /// and the documented 504 problem would never be written. Placed after it, the timeout middleware sees the cancellation first.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseHostingRequestTimeouts(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseRequestTimeouts();
    }
}
