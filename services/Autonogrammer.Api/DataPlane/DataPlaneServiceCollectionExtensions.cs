using System.Security.Cryptography;
using Autonogrammer.Api.Health;
using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.DataPlane;
using Autonogrammer.Coordination;
using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.DataPlane;

/// <summary>Registers the data plane of the API: its options and validators, the tenant hash, the schema wait, the status service, the 503 mapper and the exporter monitor.</summary>
internal static class DataPlaneServiceCollectionExtensions
{
    /// <summary>
    /// Registers the data plane. Call it after <c>AddApiCoordination</c>. The options bind from configuration when they are first read, so a configuration source
    /// added after composition (a test host) is honoured, and every section validates on start with messages that name keys.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDataPlane(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DataPlaneOptions>().BindConfiguration(DataPlaneOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<DataPlaneOptions>, DataPlaneOptionsValidator>();
        services.AddOptions<PostgresResilienceOptions>().BindConfiguration(PostgresResilienceOptions.SectionName);
        services.AddOptions<DragonflyResilienceOptions>().BindConfiguration(DragonflyResilienceOptions.SectionName);
        services.AddOptions<LedgerResilienceOptions>().BindConfiguration(LedgerResilienceOptions.SectionName);

        services.AddOptions<TenantHasherOptions>().Configure<IConfiguration>(static (options, configuration) =>
            options.Pepper = configuration[DataPlaneOptions.TenantHashPepperKey] ?? string.Empty);
        services.TryAddSingleton<ITenantHasher>(static provider =>
            CoordinationOptions.IsDragonflyStore(provider.GetRequiredService<IOptions<CoordinationOptions>>().Value.Store)
                ? new TenantHasher(provider.GetRequiredService<IOptions<TenantHasherOptions>>().Value)
                : new TenantHasher(new TenantHasherOptions { Pepper = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)) }));
        services.TryAddSingleton<ITenantKeyEraser, NoOpTenantKeyEraser>();

        services.ConfigureHttpJsonOptions(static options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, DataPlaneJsonContext.Default));

        services.TryAddSingleton<IExitCodeSink, EnvironmentExitCodeSink>();
        services.AddSingleton<SchemaHeadProbe>();
        services.AddHostedService<SchemaHeadWaitService>();

        services.AddSingleton<TelemetryExportMonitor>();
        services.AddSingleton<IHostedService>(static provider => provider.GetRequiredService<TelemetryExportMonitor>());

        services.AddSingleton<DataPlaneStatusService>();
        services.AddSingleton<IHostedService>(static provider => provider.GetRequiredService<DataPlaneStatusService>());
        services.AddSingleton<DataPlaneProblemMapper>();
        services.AddRateLimiter(static options => options.AddPolicy<string, DataPlaneStatusRateLimiterPolicy>(DataPlaneEndpoints.PolicyName));
        services.AddHealthChecks().AddCheck<DataPlaneReadinessCheck>("data-plane", tags: ["ready"]);
        Autonogrammer.Api.Audit.LedgerSinkServiceCollectionExtensions.AddLedgerSink(services);
        return services;
    }
}
