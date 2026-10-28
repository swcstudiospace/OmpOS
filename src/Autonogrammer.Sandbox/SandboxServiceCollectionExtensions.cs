using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Autonogrammer.Sandbox;

/// <summary>
/// Dependency-injection registration for sandbox services.
/// </summary>
public static class SandboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the production sandbox services (argv-only <see cref="IProcessRunner"/>).
    /// An <see cref="IProcessRunner"/> registered earlier by the host takes precedence.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSandboxRunner(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        return services;
    }
}
