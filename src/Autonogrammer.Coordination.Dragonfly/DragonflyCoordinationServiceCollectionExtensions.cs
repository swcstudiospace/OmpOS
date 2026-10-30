using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>Registers the Dragonfly coordination implementations.</summary>
public static class DragonflyCoordinationServiceCollectionExtensions
{
    /// <summary>
    /// Registers one singleton <see cref="IConnectionMultiplexer"/> built from <paramref name="connectionString"/>, plus
    /// <see cref="ISessionRunLeases"/>, <see cref="ISessionSignals"/> and <see cref="IIdempotencyStore"/> over it.
    /// </summary>
    /// <remarks>
    /// The connection string is passed to StackExchange.Redis unchanged, so it should include <c>abortConnect=false</c>.
    /// Otherwise startup fails when Dragonfly is unreachable. The string may contain a password and is never logged.
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">A StackExchange.Redis configuration string.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDragonflyCoordination(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<ISessionRunLeases, DragonflySessionRunLeases>();
        services.AddSingleton<ISessionSignals, DragonflySessionSignals>();
        services.AddSingleton<IIdempotencyStore, DragonflyIdempotencyStore>();
        return services;
    }
}
