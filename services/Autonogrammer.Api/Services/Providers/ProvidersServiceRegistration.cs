using Autonogrammer.Api.Endpoints;
using Autonogrammer.Contracts.Providers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>Registers the services behind the provider endpoints.</summary>
internal static class ProvidersServiceRegistration
{
    /// <summary>
    /// Binds and validates <c>Providers:*</c> on start, adds the provider JSON context to the HTTP JSON resolver chain, registers the access to
    /// omp, the state reader, the custom endpoint ledger, the live-test probe and the rate-limit policy of the provider endpoints. Call it once,
    /// after the harness options are registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddProviderServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.ConfigureHttpJsonOptions(static options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ProvidersJsonContext.Default));
        _ = services.AddOptions<ProvidersOptions>()
            .BindConfiguration(ProvidersOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(static options => options.PathsAreAbsolute(), "Providers:OmpPath and Providers:AgentDirectory must be absolute paths when set.")
            .ValidateOnStart();
        services.TryAddSingleton<IProviderEnvironment, ProcessProviderEnvironment>();
        services.TryAddSingleton<IProviderBackend, OmpProviderBackend>();
        services.TryAddSingleton<IProviderProbe, HttpProviderProbe>();
        services.TryAddSingleton<ProviderStateReader>();
        services.TryAddSingleton<CustomProviderLedger>();
        _ = services.AddKeyedSingleton<IRateLimiterPolicy<string>, ProviderRateLimiterPolicy>(ProvidersEndpoints.RateLimitPolicy);
        _ = services.AddOptions<RateLimiterOptions>().Configure<IServiceProvider>(static (options, provider) =>
            options.AddPolicy(ProvidersEndpoints.RateLimitPolicy, provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(ProvidersEndpoints.RateLimitPolicy)));
        return services;
    }
}
