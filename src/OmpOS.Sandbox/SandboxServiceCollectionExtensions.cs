using Microsoft.Extensions.DependencyInjection;

namespace OmpOS.Sandbox;

/// <summary>
/// Dependency-injection registration for sandbox services.
/// </summary>
public static class SandboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the production sandbox services (argv-only <see cref="IProcessRunner"/>).
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSandboxRunner(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        return services;
    }
}
