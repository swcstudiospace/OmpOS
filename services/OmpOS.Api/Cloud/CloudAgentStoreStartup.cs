using Microsoft.Extensions.Options;
using Npgsql;
using OmpOS.Api.Cloud.Options;
using OmpOS.Api.Options;
using OmpOS.Sessions.Postgres;

namespace OmpOS.Api.Cloud;

/// <summary>
/// Startup of the cloud agent store. When cloud is on and <c>Sessions:Store</c> is <c>Postgres</c>, <see cref="StartingAsync"/> creates the
/// <c>cloud_agents</c> table (idempotent) before the web server listens, so no request can run before it exists. Otherwise it does nothing.
/// </summary>
internal sealed class CloudAgentStoreStartup : IHostedLifecycleService
{
    private readonly IServiceProvider _services;
    private readonly IOptions<CloudOptions> _cloud;
    private readonly IOptions<SessionsOptions> _sessions;

    /// <summary>Initializes a new instance of the <see cref="CloudAgentStoreStartup"/> class.</summary>
    /// <param name="services">The service provider, used to resolve the data source only for the PostgreSQL store.</param>
    /// <param name="cloud">The cloud options.</param>
    /// <param name="sessions">The sessions options, which choose the store.</param>
    public CloudAgentStoreStartup(IServiceProvider services, IOptions<CloudOptions> cloud, IOptions<SessionsOptions> sessions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(cloud);
        ArgumentNullException.ThrowIfNull(sessions);
        _services = services;
        _cloud = cloud;
        _sessions = sessions;
    }

    /// <inheritdoc />
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (!_cloud.Value.Enabled || !SessionsOptions.IsPostgresStore(_sessions.Value.Store))
        {
            return;
        }

        await PostgresCloudAgentSchema.EnsureCreatedAsync(_services.GetRequiredService<NpgsqlDataSource>(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
