using Microsoft.Extensions.Options;
using Npgsql;
using OmpOS.Api.Options;
using OmpOS.Sessions.Postgres;

namespace OmpOS.Api.Persistence;

/// <summary>
/// Fail-fast startup for the PostgreSQL session store. When <c>Workflows:Enabled</c> is true, <see cref="StartingAsync"/> resolves
/// the data source, which throws naming <c>Sessions:Postgres:ConnectionString</c> when that key is missing, and creates the
/// session schema (idempotent). The host runs every service's <see cref="StartingAsync"/> before any <c>StartAsync</c>, and the
/// web server listens only in its <c>StartAsync</c>, so no request can run before the schema exists. A failure stops startup.
/// When workflows are disabled it does nothing.
/// </summary>
internal sealed class SessionStoreStartup : IHostedLifecycleService
{
    private readonly IServiceProvider _services;
    private readonly IOptions<WorkflowsOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="SessionStoreStartup"/> class.</summary>
    /// <param name="services">The application service provider, used to resolve the data source only when enabled.</param>
    /// <param name="options">The workflows options; reading them runs their validation.</param>
    public SessionStoreStartup(IServiceProvider services, IOptions<WorkflowsOptions> options)
    {
        _services = services;
        _options = options;
    }

    /// <inheritdoc />
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.Enabled)
        {
            return;
        }

        var dataSource = _services.GetRequiredService<NpgsqlDataSource>();
        await PostgresSessionSchema.EnsureCreatedAsync(dataSource, cancellationToken).ConfigureAwait(false);
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
