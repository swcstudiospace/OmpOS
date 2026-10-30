using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Autonogrammer.Cowork.Artifacts.Stores;

/// <summary>Registers artifact stores. A host that registers <c>IArtifactStore</c> first keeps its instance.</summary>
public static class StoresServiceCollectionExtensions
{
    /// <summary>Registers the desktop file store unless the host already registered <c>IArtifactStore</c>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="stateRoot">The state directory.</param>
    /// <param name="accountId">The account tenant.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddFileArtifactStore(this IServiceCollection services, string stateRoot, string accountId)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IArtifactStore>(_ => new FileArtifactStore(stateRoot, accountId));
        return services;
    }

    /// <summary>
    /// Registers the Postgres store over the host <see cref="NpgsqlDataSource"/> unless the host already registered <c>IArtifactStore</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="accountId">The account every query filters on.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddPostgresArtifactStore(this IServiceCollection services, string accountId)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IArtifactStore>(provider => new PostgresArtifactStore(provider.GetRequiredService<NpgsqlDataSource>(), accountId));
        return services;
    }

    /// <summary>Registers sync and its remote unless the host already registered them. Sync stays off unless options say otherwise.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddArtifactSync(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<HttpArtifactRemote>();
        services.TryAddSingleton<ArtifactSyncService>();
        return services;
    }
}
