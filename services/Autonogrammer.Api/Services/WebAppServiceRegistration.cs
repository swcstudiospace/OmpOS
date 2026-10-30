using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Controls;
using Autonogrammer.Harness.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Autonogrammer.Api.Services;

/// <summary>Registers the services behind the web-app endpoints: catalog, event stream, stages, steering, configuration, change review and the source offer.</summary>
internal static class WebAppServiceRegistration
{
    /// <summary>
    /// Registers every service the web-app endpoints use, adds their source-generated JSON contexts to the HTTP JSON resolver chain, and wraps the
    /// registered <see cref="IHarnessSessionFactory"/> so the engine session of each run is reachable. Call it after the harness factory is registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSessionWebServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, CatalogJsonContext.Default);
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, SessionControlsJsonContext.Default);
        });
        _ = services.AddOptions<SessionWorkspaceOptions>().BindConfiguration(SessionWorkspaceOptions.SectionName);
        services.TryAddSingleton<SessionLifecycleStore>();
        services.TryAddSingleton<WorkspaceLocator>();
        services.TryAddSingleton<ChangeDecisionStore>();
        services.TryAddSingleton<SessionLogAppender>();
        services.TryAddSingleton<CatalogEventHub>();
        services.TryAddSingleton<StageHub>();
        services.TryAddSingleton<SourceInfoProvider>();
        services.TryAddSingleton<IWorkspacePluginCheck, UnavailablePluginCheck>();
        services.TryAddSingleton<CatalogChangeDetector>();
        services.AddSingleton<IHostedService>(static provider => provider.GetRequiredService<CatalogChangeDetector>());

        return services.AddLiveHarnessTracking();
    }

    /// <summary>
    /// Wraps the currently registered <see cref="IHarnessSessionFactory"/> in a <see cref="TrackingHarnessSessionFactory"/>. The wrapper returns the
    /// inner factory's engine session unchanged and tells <see cref="LiveSessionTracker"/> about it. Call it once per factory registration, after that registration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLiveHarnessTracking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<LiveSessionTracker>();
        var inner = services.LastOrDefault(static descriptor => descriptor.ServiceType == typeof(IHarnessSessionFactory) && !descriptor.IsKeyedService)
            ?? throw new InvalidOperationException("Register the IHarnessSessionFactory before AddLiveHarnessTracking.");
        _ = services.Remove(inner);
        services.Add(ServiceDescriptor.Describe(
            typeof(IHarnessSessionFactory),
            provider => new TrackingHarnessSessionFactory(Create(provider, inner), provider.GetRequiredService<LiveSessionTracker>()),
            inner.Lifetime));
        return services;
    }

    private static IHarnessSessionFactory Create(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IHarnessSessionFactory instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return (IHarnessSessionFactory)factory(provider);
        }

        return (IHarnessSessionFactory)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
    }
}
