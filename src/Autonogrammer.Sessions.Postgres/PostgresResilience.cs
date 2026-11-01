using Autonogrammer.Resilience;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// The PostgreSQL <see cref="ResiliencePipeline"/> of one process: one deadline, retry policy, circuit breaker and pair of bulkheads for every
/// command the session store, the cloud agent store, the account erasure and the migration runner send. The stores share one instance, so one
/// failing database opens one breaker.
/// </summary>
/// <remarks>
/// The component name of the pipeline is <c>postgres</c>. A deadline, an open breaker or a full bulkhead surface as
/// <see cref="DataPlaneTimeoutException"/>; the caller's own cancellation passes through untouched.
/// </remarks>
public sealed class PostgresResilience
{
    /// <summary>Initializes a new instance of the <see cref="PostgresResilience"/> class.</summary>
    /// <param name="pipeline">The pipeline, normally built by <see cref="Create"/>.</param>
    public PostgresResilience(ResiliencePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        Pipeline = pipeline;
    }

    /// <summary>Gets the pipeline every PostgreSQL command runs through.</summary>
    public ResiliencePipeline Pipeline { get; }

    /// <summary>Builds the pipeline of the PostgreSQL component from its options.</summary>
    /// <param name="options">The options; <c>DataPlane:Postgres:Resilience</c> in the API.</param>
    /// <param name="timeProvider">The clock for deadlines, retry delays and the breaker, or <see langword="null"/> for the system clock.</param>
    /// <param name="logger">The logger, or <see langword="null"/>.</param>
    /// <returns>A new pipeline holder.</returns>
    public static PostgresResilience Create(PostgresResilienceOptions options, TimeProvider? timeProvider = null, ILogger<ResiliencePipeline>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new PostgresResilience(new ResiliencePipeline(options.ToPolicy(IsTransient, IsSerializationFailure), timeProvider ?? TimeProvider.System, null, logger));
    }

    /// <summary>Builds a pipeline with the default options of D-41.</summary>
    /// <returns>A new pipeline holder.</returns>
    internal static PostgresResilience CreateDefault() => Create(new PostgresResilienceOptions());

    /// <summary>
    /// Says whether a failure is a transient failure of the database connection: one the breaker counts and a retry of a read may cure.
    /// A failure the server answered (a constraint violation, a serialisation failure, a deadlock) is not transient, because the database is
    /// healthy; only the answers that say the server is going away or full are.
    /// </summary>
    /// <param name="exception">The failure to classify.</param>
    /// <returns><see langword="true"/> when the failure is transient.</returns>
    internal static bool IsTransient(Exception exception) => exception switch
    {
        // 53300 too_many_connections, 57P01 admin_shutdown, 57P02 crash_shutdown, 57P03 cannot_connect_now.
        PostgresException postgres => postgres.SqlState is "53300" or "57P01" or "57P02" or "57P03",
        NpgsqlException npgsql => npgsql.IsTransient,
        _ => ResiliencePolicy.DefaultIsTransient(exception),
    };

    /// <summary>Says whether a failure is a serialisation failure or a deadlock, which the whole transaction may answer with one retry.</summary>
    /// <param name="exception">The failure to classify.</param>
    /// <returns><see langword="true"/> for SQLSTATE 40001 and 40P01.</returns>
    internal static bool IsSerializationFailure(Exception exception) =>
        exception is PostgresException { SqlState: "40001" or "40P01" };
}
