using Microsoft.Extensions.DependencyInjection;

namespace Autonogrammer.Coordination;

/// <summary>Registers the in-process coordination implementations.</summary>
public static class InMemoryCoordinationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISessionRunLeases"/>, <see cref="ISessionSignals"/> and <see cref="IIdempotencyStore"/> as
    /// singletons backed by the in-memory implementations, which read <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddInMemoryCoordination(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISessionRunLeases>(_ => new InMemorySessionRunLeases(TimeProvider.System));
        services.AddSingleton<ISessionSignals>(_ => new InMemorySessionSignals());
        services.AddSingleton<IIdempotencyStore>(_ => new InMemoryIdempotencyStore(TimeProvider.System));
        return services;
    }
}
