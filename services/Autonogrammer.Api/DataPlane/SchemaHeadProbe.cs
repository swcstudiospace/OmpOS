using Autonogrammer.Api.Options;
using Autonogrammer.Sessions.Postgres;
using Autonogrammer.Sessions.Postgres.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Compares every compiled migration with the applied ledger. The answer is cached for <see cref="CacheFor"/> so a health probe and a status
/// request do not each send a query, and the last known answer is readable without I/O for the metric gauge. The read runs through the PostgreSQL
/// resilience pipeline under the runtime role, which may read the ledger table.
/// </summary>
internal sealed class SchemaHeadProbe
{
    /// <summary>Gets how long an answer is reused.</summary>
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(5);

    private readonly Func<CancellationToken, Task<IReadOnlyList<AppliedMigration>>> _readApplied;
    private readonly IReadOnlyDictionary<int, string> _required;
    private readonly int _head;
    private readonly bool _applies;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private SchemaState? _last;
    private DateTimeOffset _lastAt;

    /// <summary>Initializes a new instance of the <see cref="SchemaHeadProbe"/> class over the PostgreSQL store of the host.</summary>
    /// <param name="services">The service provider that holds the data source; it is resolved only when the session store is PostgreSQL.</param>
    /// <param name="sessions">The sessions options.</param>
    /// <param name="time">The clock.</param>
    public SchemaHeadProbe(IServiceProvider services, IOptions<SessionsOptions> sessions, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(sessions);
        _applies = SessionsOptions.IsPostgresStore(sessions.Value.Store);
        _required = MigrationRegistry.All.ToDictionary(static step => step.Version, MigrationRunner.ChecksumOf);
        _head = MigrationRegistry.Head;
        _time = time;
        MigrationRunner? runner = null;
        _readApplied = cancellationToken =>
        {
            runner ??= new MigrationRunner(services.GetRequiredService<NpgsqlDataSource>(), resilience: services.GetRequiredService<PostgresResilience>());
            return runner.ListAppliedAsync(cancellationToken);
        };
    }

    /// <summary>Initializes a new instance of the <see cref="SchemaHeadProbe"/> class over an explicit ledger reader and required steps.</summary>
    /// <param name="readApplied">Reads the applied ledger.</param>
    /// <param name="requiredSteps">The migrations compiled into the binary.</param>
    /// <param name="applies">Whether the store is PostgreSQL.</param>
    /// <param name="time">The clock.</param>
    public SchemaHeadProbe(Func<CancellationToken, Task<IReadOnlyList<AppliedMigration>>> readApplied, IReadOnlyList<IMigrationStep> requiredSteps, bool applies, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(readApplied);
        ArgumentNullException.ThrowIfNull(requiredSteps);
        if (requiredSteps.Count == 0)
        {
            throw new ArgumentException("At least one required migration is needed.", nameof(requiredSteps));
        }

        _readApplied = readApplied;
        _required = requiredSteps.ToDictionary(static step => step.Version, MigrationRunner.ChecksumOf);
        _head = _required.Count == 0 ? 0 : _required.Keys.Max();
        _applies = applies;
        _time = time;
    }

    /// <summary>Gets the answer of the last successful probe without any I/O, or <see langword="null"/> when none ran yet.</summary>
    public SchemaState? Last
    {
        get
        {
            lock (_gate)
            {
                return _applies ? _last : SchemaState.NotApplicable;
            }
        }
    }

    /// <summary>Compares the schema with the compiled head.</summary>
    /// <param name="refresh">Ignore the cached answer.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state of the schema.</returns>
    /// <exception cref="Autonogrammer.Resilience.DataPlaneTimeoutException">The store did not answer in time or its breaker is open.</exception>
    public async Task<SchemaState> GetAsync(bool refresh, CancellationToken cancellationToken)
    {
        if (!_applies)
        {
            return SchemaState.NotApplicable;
        }

        lock (_gate)
        {
            if (!refresh && _last is { } cached && _time.GetUtcNow() - _lastAt < CacheFor)
            {
                return cached;
            }
        }

        var applied = await _readApplied(cancellationToken).ConfigureAwait(false);
        var recorded = applied.ToDictionary(static migration => migration.Version, static migration => migration.Checksum);
        var complete = _required.All(step => recorded.TryGetValue(step.Key, out var checksum) && string.Equals(step.Value, checksum, StringComparison.Ordinal));
        var version = applied.Count == 0 ? 0 : applied.Max(static migration => migration.Version);
        var state = !complete ? SchemaState.Behind : version > _head ? SchemaState.Ahead : SchemaState.Current;
        lock (_gate)
        {
            _last = state;
            _lastAt = _time.GetUtcNow();
        }

        return state;
    }
}
