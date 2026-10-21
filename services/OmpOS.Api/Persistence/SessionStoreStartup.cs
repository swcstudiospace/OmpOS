using Microsoft.Extensions.Options;
using Npgsql;
using OmpOS.Api.Options;
using OmpOS.Sessions.Postgres;

namespace OmpOS.Api.Persistence;

/// <summary>
/// Startup for the session store. When <c>Sessions:Store</c> is <c>Postgres</c>, <see cref="StartingAsync"/> resolves the data source,
/// which throws naming <c>Sessions:Postgres:ConnectionString</c> when that key is missing, and creates the session schema (idempotent).
/// The host runs every service's <see cref="StartingAsync"/> before any <c>StartAsync</c>, and the web server listens only in its
/// <c>StartAsync</c>, so no request can run before the schema exists. A failure stops startup. When the store is <c>InMemory</c> it
/// creates nothing; in the Production environment it logs a warning, which does not stop startup.
/// </summary>
internal sealed class SessionStoreStartup : IHostedLifecycleService
{
    private static readonly Action<ILogger, Exception?> InMemoryInProduction =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(31, "SessionsInMemoryInProduction"),
            "Sessions:Store is InMemory in the Production environment. Session state is not durable and is not shared between instances; set Sessions:Store to Postgres.");

    private readonly IServiceProvider _services;
    private readonly IOptions<SessionsOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SessionStoreStartup> _logger;

    /// <summary>Initializes a new instance of the <see cref="SessionStoreStartup"/> class.</summary>
    /// <param name="services">The application service provider, used to resolve the data source only for the PostgreSQL store.</param>
    /// <param name="options">The sessions options; reading them runs their validation.</param>
    /// <param name="environment">The host environment, used to decide whether the in-memory warning applies.</param>
    /// <param name="logger">The logger.</param>
    public SessionStoreStartup(
        IServiceProvider services,
        IOptions<SessionsOptions> options,
        IHostEnvironment environment,
        ILogger<SessionStoreStartup> logger)
    {
        _services = services;
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (!SessionsOptions.IsPostgresStore(_options.Value.Store))
        {
            if (_environment.IsProduction())
            {
                InMemoryInProduction(_logger, null);
            }

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
