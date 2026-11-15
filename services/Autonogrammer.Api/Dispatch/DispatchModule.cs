using Autonogrammer.Api.Accounts;
using Autonogrammer.Api.Dispatch.Devices;
using Autonogrammer.Api.Dispatch.Store;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Coordination;
using Autonogrammer.Sessions.Postgres;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.Dispatch;

/// <summary>Registers dispatch services. The feature is off by default. The store is chosen by Dispatch:Store; the in-memory store is Development only.</summary>
internal static class DispatchModule
{
    /// <summary>Registers options, the device scheme, the store and the rate-limit policies.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The host environment. Development is the only environment that may select the in-memory store.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDispatchServices(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(DeviceEndpoints.DispatchEndpointsMapped)))
        {
            return services;
        }

        _ = services.AddOptions<DispatchOptions>().BindConfiguration(DispatchOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<DispatchOptions>, DispatchOptionsValidator>();
        AddDispatchJson(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DeviceEndpoints.DispatchEndpointsMapped>();
        services.AddSingleton<IAccountFeature, DispatchFeature>();
        services.AddSingleton<DispatchMetrics>();
        services.AddSingleton(static provider =>
        {
            var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value.Limits;
            return new DispatchStoreLimits(options.MaxQueuedPerDevice, options.DevicesPerAccount, options.FeedRetention, options.BodyRetention);
        });
        AddDispatchStore(services, environment.IsDevelopment());

        services.AddSingleton<DeviceCredentialIssuer>();
        services.AddSingleton<DeviceCredentialValidator>();
        services.TryAddSingleton<IDispatchConnectHandler, NotReadyDispatchConnectHandler>();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, DispatchDeviceScheme.DispatchDeviceAuthenticationHandler>(DispatchDeviceScheme.Name, displayName: null, static _ => { });
        services.AddAuthorizationBuilder().AddPolicy(DispatchDeviceScheme.PolicyName, static policy =>
        {
            policy.AddAuthenticationSchemes(DispatchDeviceScheme.Name);
            policy.RequireAuthenticatedUser();
        });

        services.TryAddKeyedSingleton<IRateLimiterPolicy<string>, DispatchRateLimits.DispatchPairingRateLimiterPolicy>(DispatchRateLimits.DispatchPairingRateLimiterPolicy.PolicyName);
        services.TryAddKeyedSingleton<IRateLimiterPolicy<string>, DispatchRateLimits.DispatchConnectRateLimiterPolicy>(DispatchRateLimits.DispatchConnectRateLimiterPolicy.PolicyName);
        services.TryAddKeyedSingleton<IRateLimiterPolicy<string>, DispatchRateLimits.DispatchCommandRateLimiterPolicy>(DispatchRateLimits.DispatchCommandRateLimiterPolicy.PolicyName);
        services.TryAddKeyedSingleton<IRateLimiterPolicy<string>, DispatchRateLimits.DispatchReadRateLimiterPolicy>(DispatchRateLimits.DispatchReadRateLimiterPolicy.PolicyName);
        _ = services.AddOptions<RateLimiterOptions>().Configure<IServiceProvider>(static (options, provider) =>
        {
            options.AddPolicy(
                DispatchRateLimits.DispatchPairingRateLimiterPolicy.PolicyName,
                provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(DispatchRateLimits.DispatchPairingRateLimiterPolicy.PolicyName));
            options.AddPolicy(
                DispatchRateLimits.DispatchConnectRateLimiterPolicy.PolicyName,
                provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(DispatchRateLimits.DispatchConnectRateLimiterPolicy.PolicyName));
            options.AddPolicy(
                DispatchRateLimits.DispatchCommandRateLimiterPolicy.PolicyName,
                provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(DispatchRateLimits.DispatchCommandRateLimiterPolicy.PolicyName));
            options.AddPolicy(
                DispatchRateLimits.DispatchReadRateLimiterPolicy.PolicyName,
                provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(DispatchRateLimits.DispatchReadRateLimiterPolicy.PolicyName));
        });
        return services;
    }

    /// <summary>
    /// Registers <see cref="IDispatchStore"/>. The store is chosen when it is first resolved, from the bound <c>Dispatch:Store</c>, so the
    /// configuration the host runs with decides it. <c>Postgres</c> is used in every environment; <c>InMemory</c> only in Development.
    /// Outside Development the validator refuses start unless the store is Postgres, so the refusal below is not reached by a started host.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="development">Whether the host runs in Development.</param>
    private static void AddDispatchStore(IServiceCollection services, bool development)
    {
        services.TryAddSingleton<IDispatchStore>(provider => CreateDispatchStore(provider, development));
    }

    /// <summary>Creates the store the options select.</summary>
    /// <param name="provider">The service provider.</param>
    /// <param name="development">Whether the host runs in Development.</param>
    /// <returns>The store.</returns>
    private static IDispatchStore CreateDispatchStore(IServiceProvider provider, bool development)
    {
        var limits = provider.GetRequiredService<DispatchStoreLimits>();
        var time = provider.GetRequiredService<TimeProvider>();
        if (provider.GetRequiredService<IOptions<DispatchOptions>>().Value.Store == DispatchOptions.DispatchStoreKind.Postgres)
        {
            return new PostgresDispatchStore(
                provider.GetRequiredService<NpgsqlDataSource>(),
                limits,
                time,
                provider.GetRequiredService<ITenantHasher>().HashTenant,
                provider.GetRequiredService<PostgresResilience>());
        }

        return development
            ? new InMemoryDispatchStore(limits, time)
            : throw new InvalidOperationException("Dispatch:Store must be Postgres outside Development. DispatchOptionsValidator refuses start otherwise.");
    }

    /// <summary>Puts the dispatch JSON contexts first in the HTTP JSON chain, so the controller and device bodies serialise and the OpenAPI document can describe them.</summary>
    /// <param name="services">The service collection.</param>
    private static void AddDispatchJson(IServiceCollection services)
    {
        _ = services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, DispatchJsonContext.Default);
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, DeviceEndpoints.DispatchApiJsonContext.Default);
        });
    }

    /// <summary>The account-menu feature key <c>dispatch</c>. It is listed only while <c>Dispatch:Enabled</c> is true.</summary>
    internal sealed class DispatchFeature : IAccountFeature
    {
        private readonly IOptions<DispatchOptions> _options;

        /// <summary>Initializes a new instance of the <see cref="DispatchFeature"/> class.</summary>
        /// <param name="options">The dispatch options.</param>
        public DispatchFeature(IOptions<DispatchOptions> options)
        {
            _options = options;
        }

        /// <inheritdoc />
        public string Key => "dispatch";

        /// <inheritdoc />
        public bool Enabled => _options.Value.Enabled;
    }
}
