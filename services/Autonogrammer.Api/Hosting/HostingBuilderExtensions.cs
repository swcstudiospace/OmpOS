using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Hosting;

/// <summary>Service and server registration for production hosting behind a TLS-terminating edge.</summary>
internal static class HostingBuilderExtensions
{
    private static readonly TimeSpan MinRequestBodyGracePeriod = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Binds and validates <see cref="HostingOptions"/>; configures Kestrel limits, forwarded headers, HSTS, request timeouts and
    /// the shutdown timeout; registers the startup gate with the startup and shutdown health checks; and selects JSON console logs.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddHostingHardening(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        services.AddOptions<HostingOptions>()
            .BindConfiguration(HostingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<HostingOptions>, HostingOptionsValidator>();

        builder.WebHost.ConfigureKestrel(static (context, kestrel) =>
        {
            kestrel.AddServerHeader = false;
            var hosting = context.Configuration.GetSection(HostingOptions.SectionName).Get<HostingOptions>() ?? new HostingOptions();
            if (!HostingOptionsValidator.IsValid(hosting))
            {
                // Startup validation reports the invalid value and stops the host; applying it here would only fail earlier and less clearly.
                return;
            }

            var limits = hosting.Kestrel;
            kestrel.Limits.MaxRequestBodySize = limits.MaxRequestBodyBytes;
            kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(limits.RequestHeadersTimeoutSeconds);
            kestrel.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(limits.KeepAliveTimeoutSeconds);
            kestrel.Limits.MaxConcurrentConnections = limits.MaxConcurrentConnections;
            kestrel.Limits.MinRequestBodyDataRate = limits.MinRequestBodyDataRateBytesPerSecond == 0
                ? null
                : new MinDataRate(limits.MinRequestBodyDataRateBytesPerSecond, MinRequestBodyGracePeriod);
        });

        services.AddSingleton<IConfigureOptions<ForwardedHeadersOptions>, ConfigureForwardedHeadersOptions>();
        services.AddHsts(static _ => { });
        services.AddOptions<HstsOptions>().Configure<IOptions<HostingOptions>>(static (hsts, hosting) =>
            hsts.MaxAge = TimeSpan.FromDays(hosting.Value.Hsts.MaxAgeDays));
        services.AddProblemDetails();
        services.AddRequestTimeouts();
        services.AddSingleton<IConfigureOptions<RequestTimeoutOptions>, ConfigureRequestTimeoutOptions>();
        services.AddOptions<HostOptions>().Configure<IOptions<HostingOptions>>(static (host, hosting) =>
            host.ShutdownTimeout = TimeSpan.FromSeconds(hosting.Value.ShutdownTimeoutSeconds));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<HostingStartupGate>();
        services.AddHostedService<StartupGateHostedService>();
        services.AddHealthChecks()
            .AddCheck<HostingStartupCheck>("startup", tags: [HostingPolicies.StartupTag])
            .AddCheck<HostingPendingShutdownCheck>("shutdown", tags: [HostingPolicies.ReadyTag]);

        var jsonLogs = builder.Configuration.GetValue<bool?>(HostingPolicies.JsonLogsKey) ?? !builder.Environment.IsDevelopment();
        if (jsonLogs)
        {
            builder.Logging.AddJsonConsole(static options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
            });
        }

        return builder;
    }
}
