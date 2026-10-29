using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Hosting;

/// <summary>Pipeline registration for the hosting middleware. Call it first in the pipeline.</summary>
internal static class HostingApplicationExtensions
{
    /// <summary>
    /// Adds forwarded-header handling (unless the mode is <see cref="ForwardedHeadersMode.None"/>), HSTS (outside Development),
    /// the security-header middleware and request timeouts.
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

        if (!environment.IsDevelopment() && options.Hsts.Enabled)
        {
            _ = app.UseHsts();
        }

        if (options.SecurityHeaders.Enabled)
        {
            _ = app.UseMiddleware<SecurityHeadersMiddleware>();
        }

        return app.UseRequestTimeouts();
    }
}
