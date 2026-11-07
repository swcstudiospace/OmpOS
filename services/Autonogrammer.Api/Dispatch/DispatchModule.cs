using Autonogrammer.Api.Accounts;
using Autonogrammer.Api.Dispatch.Devices;
using Autonogrammer.Api.Dispatch.Store;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Dispatch;

/// <summary>Registers dispatch services. The feature is off by default. The in-memory store is registered only in Development.</summary>
internal static class DispatchModule
{
    /// <summary>Registers options, the device scheme, the store and the rate-limit policies.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The host environment. The in-memory store is registered only in Development.</param>
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
        if (environment.IsDevelopment())
        {
            services.TryAddSingleton<IDispatchStore>(static provider => new InMemoryDispatchStore(
                provider.GetRequiredService<DispatchStoreLimits>(),
                provider.GetRequiredService<TimeProvider>()));
        }

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
