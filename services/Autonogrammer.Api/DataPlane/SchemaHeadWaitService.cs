using Autonogrammer.Api.Options;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Waits for the PostgreSQL schema to reach the compiled migration head (D-58). It is a background service, not a startup step, so the host listens and
/// validates its options while it waits and <see cref="Autonogrammer.Api.Health.DataPlaneReadinessCheck"/> reports the schema as behind. After
/// <c>DataPlane:Postgres:SchemaWaitSeconds</c> it logs an error that names <c>Sessions:Postgres:MigratorConnectionString</c>, asks for exit code 1 and
/// stops the application. A schema that is newer than the head is accepted. It does nothing when the session store is not PostgreSQL.
/// </summary>
internal sealed partial class SchemaHeadWaitService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly SchemaHeadProbe _probe;
    private readonly IOptions<DataPlaneOptions> _options;
    private readonly IOptions<SessionsOptions> _sessions;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IExitCodeSink _exit;
    private readonly TimeProvider _time;
    private readonly ILogger<SchemaHeadWaitService> _logger;

    /// <summary>Initializes a new instance of the <see cref="SchemaHeadWaitService"/> class.</summary>
    /// <param name="probe">Compares the schema with the head.</param>
    /// <param name="options">The data-plane options.</param>
    /// <param name="sessions">The sessions options.</param>
    /// <param name="lifetime">Stops the application when the wait ends.</param>
    /// <param name="exit">Receives the exit code.</param>
    /// <param name="time">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SchemaHeadWaitService(
        SchemaHeadProbe probe,
        IOptions<DataPlaneOptions> options,
        IOptions<SessionsOptions> sessions,
        IHostApplicationLifetime lifetime,
        IExitCodeSink exit,
        TimeProvider time,
        ILogger<SchemaHeadWaitService> logger)
    {
        _probe = probe;
        _options = options;
        _sessions = sessions;
        _lifetime = lifetime;
        _exit = exit;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!SessionsOptions.IsPostgresStore(_sessions.Value.Store))
        {
            return;
        }

        var wait = TimeSpan.FromSeconds(_options.Value.Postgres.SchemaWaitSeconds);
        var deadline = _time.GetUtcNow() + wait;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var behind = true;
                try
                {
                    behind = await _probe.GetAsync(refresh: true, stoppingToken).ConfigureAwait(false) == SchemaState.Behind;
                }
                catch (Exception ex) when (ex is Autonogrammer.Resilience.DataPlaneTimeoutException or Npgsql.NpgsqlException)
                {
                    // The store is not reachable yet: the wait covers a database that starts after the API as well as a migration that has not run.
                    LogProbeFailed(_logger, ex.GetType().Name);
                }

                if (!behind)
                {
                    LogSchemaReady(_logger);
                    return;
                }

                if (_time.GetUtcNow() >= deadline)
                {
                    LogSchemaWaitExpired(_logger, DataPlanePostgresOptions.SchemaWaitSecondsKey, Autonogrammer.Sessions.Postgres.ServiceCollectionExtensions.MigratorConnectionStringKey);
                    _exit.Set(1);
                    _lifetime.StopApplication();
                    return;
                }

                await Task.Delay(PollInterval, _time, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; nothing is left to wait for.
        }
    }

    [LoggerMessage(EventId = 6841, Level = LogLevel.Information, Message = "The PostgreSQL schema is at or ahead of the compiled migration head.")]
    private static partial void LogSchemaReady(ILogger logger);

    [LoggerMessage(EventId = 6842, Level = LogLevel.Warning, Message = "The schema check failed ({ExceptionType}); retrying until the wait ends.")]
    private static partial void LogProbeFailed(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 6843, Level = LogLevel.Error, Message = "The PostgreSQL schema did not reach the compiled migration head within {WaitKey}. Run the migrate service with {MigratorKey} set, then start the API again. The API stops.")]
    private static partial void LogSchemaWaitExpired(ILogger logger, string waitKey, string migratorKey);
}
