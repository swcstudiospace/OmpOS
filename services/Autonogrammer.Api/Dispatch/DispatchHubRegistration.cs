using Autonogrammer.Api.Dispatch.Devices;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Autonogrammer.Api.Dispatch;

/// <summary>Registers the relay hub. It replaces the not-ready connect handler, so the connect route that is already mapped serves the hub.</summary>
internal static class DispatchHubRegistration
{
    /// <summary>Registers the hub, its session registry and the connect handler that serves them.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDispatchHub(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<DispatchSessionRegistry>();
        services.AddSingleton<DispatchHub>();
        services.Replace(ServiceDescriptor.Singleton<IDispatchConnectHandler>(static provider => provider.GetRequiredService<DispatchHub>()));
        return services;
    }
}
