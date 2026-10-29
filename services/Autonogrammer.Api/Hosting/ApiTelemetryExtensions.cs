using System.Reflection;
using Autonogrammer.Api.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Autonogrammer.Api.Hosting;

/// <summary>OpenTelemetry registration for the API host.</summary>
internal static class ApiTelemetryExtensions
{
    /// <summary>Gets the <c>service.name</c> resource attribute.</summary>
    public const string ServiceName = "autonogrammer-api";

    /// <summary>
    /// Registers tracing and metrics with a service resource (name, version, instance id, deployment environment). The exporter
    /// comes from <c>Otel:Exporter</c>: <c>none</c> (default), <c>console</c> or <c>otlp</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The host environment.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiTelemetry(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var otelExporter = configuration.GetValue<string>("Otel:Exporter") ?? "none";
        var version = typeof(ApiTelemetry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var instanceId = $"{Environment.MachineName}-{Environment.ProcessId}";
        var deploymentEnvironment = environment.EnvironmentName;

        _ = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName, serviceVersion: version, serviceInstanceId: instanceId)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", deploymentEnvironment)]))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation().AddSource(ApiTelemetry.SourceName);
                if (otelExporter.Equals("console", StringComparison.OrdinalIgnoreCase))
                {
                    tracing.AddConsoleExporter();
                }
                else if (otelExporter.Equals("otlp", StringComparison.OrdinalIgnoreCase))
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation().AddMeter(ApiTelemetry.SourceName);
                if (otelExporter.Equals("console", StringComparison.OrdinalIgnoreCase))
                {
                    metrics.AddConsoleExporter();
                }
                else if (otelExporter.Equals("otlp", StringComparison.OrdinalIgnoreCase))
                {
                    metrics.AddOtlpExporter();
                }
            });
        return services;
    }
}
