using Autonogrammer.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>Registers the Dragonfly coordination implementations.</summary>
public static class DragonflyCoordinationServiceCollectionExtensions
{
    /// <summary>
    /// Registers one singleton <see cref="IConnectionMultiplexer"/> built from <paramref name="connectionString"/>, plus
    /// <see cref="ISessionRunLeases"/>, <see cref="ISessionSignals"/>, <see cref="IIdempotencyStore"/> and <see cref="ITenantKeyEraser"/> over it,
    /// all sharing one <see cref="DragonflyResilience"/>.
    /// </summary>
    /// <remarks>
    /// The connection string is passed to StackExchange.Redis with the operation and connect timeouts of <see cref="DragonflyResilienceOptions"/>
    /// (bound from <c>DataPlane:Dragonfly:Resilience</c> when <see cref="IOptions{TOptions}"/> is registered), so it should include <c>abortConnect=false</c>.
    /// Otherwise startup fails when Dragonfly is unreachable. The string may contain a password and is never logged.
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">A StackExchange.Redis configuration string.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDragonflyCoordination(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton(provider => DragonflyResilience.Create(
            provider.GetService<IOptions<DragonflyResilienceOptions>>()?.Value,
            provider.GetService<TimeProvider>(),
            provider.GetService<ILogger<ResiliencePipeline>>()));
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var resilience = provider.GetRequiredService<DragonflyResilience>();
            var configuration = ConfigurationOptions.Parse(connectionString);
            configuration.AsyncTimeout = resilience.Options.OpMs;
            configuration.ConnectTimeout = resilience.Options.ConnectSeconds * 1000;
            return ConnectionMultiplexer.Connect(configuration);
        });
        services.AddSingleton<ISessionRunLeases, DragonflySessionRunLeases>();
        services.AddSingleton<ISessionSignals, DragonflySessionSignals>();
        services.AddSingleton<IIdempotencyStore, DragonflyIdempotencyStore>();
        services.AddSingleton<ITenantKeyEraser, DragonflyTenantErasure>();
        return services;
    }
}
