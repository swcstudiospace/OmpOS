using Autonogrammer.Contracts.DataPlane;
using Autonogrammer.Resilience;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Maps a data-plane failure to the one 503 problem a store outage produces: <c>/problems/data_plane_unavailable</c> with the component, a closed reason and the
/// seconds to wait. It recognises a <see cref="DataPlaneTimeoutException"/> (PostgreSQL, Dragonfly or ledger) and a PostgreSQL schema that is behind the
/// code (SQLSTATE 42P01 undefined table or 3F000 invalid schema name), anywhere in the exception chain. The problem never carries the exception text.
/// </summary>
internal sealed class DataPlaneProblemMapper
{
    /// <summary>Gets the stable code of the problem.</summary>
    public const string Code = "data_plane_unavailable";

    /// <summary>Gets the problem type.</summary>
    public const string ProblemType = "/problems/" + Code;

    private const int MaxChainDepth = 8;
    private const int TimeoutRetrySeconds = 5;
    private const int SchemaBehindRetrySeconds = 15;

    private readonly IOptions<PostgresResilienceOptions> _postgres;
    private readonly IOptions<DragonflyResilienceOptions> _dragonfly;
    private readonly IOptions<LedgerResilienceOptions> _ledger;

    /// <summary>Initializes a new instance of the <see cref="DataPlaneProblemMapper"/> class.</summary>
    /// <param name="postgres">The PostgreSQL resilience options: the breaker window is the retry time while the breaker is open.</param>
    /// <param name="dragonfly">The Dragonfly resilience options.</param>
    /// <param name="ledger">The ledger resilience options.</param>
    public DataPlaneProblemMapper(
        IOptions<PostgresResilienceOptions> postgres,
        IOptions<DragonflyResilienceOptions> dragonfly,
        IOptions<LedgerResilienceOptions> ledger)
    {
        _postgres = postgres;
        _dragonfly = dragonfly;
        _ledger = ledger;
    }

    /// <summary>Maps a failure to the problem, if it is a data-plane outage.</summary>
    /// <param name="exception">The failure.</param>
    /// <param name="problem">The problem when the failure is a data-plane outage.</param>
    /// <returns><see langword="true"/> when <paramref name="problem"/> was produced.</returns>
    public bool TryMap(Exception exception, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DataPlaneProblem? problem)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var current = exception;
        for (var depth = 0; current is not null && depth < MaxChainDepth; depth++, current = current.InnerException)
        {
            switch (current)
            {
                case DataPlaneTimeoutException timeout when TryComponent(timeout.Component, out var component):
                    var reason = timeout.Reason == DataPlaneTimeoutReason.CircuitOpen ? DataPlaneReason.CircuitOpen : DataPlaneReason.Timeout;
                    problem = Build(component, reason, reason == DataPlaneReason.CircuitOpen ? BreakerSeconds(component) : TimeoutRetrySeconds);
                    return true;
                case PostgresException { SqlState: "42P01" or "3F000" }:
                    problem = Build(DataPlaneComponent.Postgres, DataPlaneReason.SchemaBehind, SchemaBehindRetrySeconds);
                    return true;
                default:
                    break;
            }
        }

        problem = null;
        return false;
    }

    private static bool TryComponent(string name, out DataPlaneComponent component)
    {
        switch (name)
        {
            case "postgres":
                component = DataPlaneComponent.Postgres;
                return true;
            case "dragonfly":
                component = DataPlaneComponent.Dragonfly;
                return true;
            case "ledger":
                component = DataPlaneComponent.Ledger;
                return true;
            default:
                component = default;
                return false;
        }
    }

    private static DataPlaneProblem Build(DataPlaneComponent component, DataPlaneReason reason, int retryAfterSeconds) => new(
        ProblemType,
        "A backing service is not available.",
        StatusCodes.Status503ServiceUnavailable,
        "A backing service of Autonogrammer is not reachable right now. Try again after the time in the Retry-After header.",
        component,
        reason,
        retryAfterSeconds);

    private int BreakerSeconds(DataPlaneComponent component) => component switch
    {
        DataPlaneComponent.Postgres => _postgres.Value.BreakerOpenSeconds,
        DataPlaneComponent.Dragonfly => _dragonfly.Value.BreakerOpenSeconds,
        _ => _ledger.Value.BreakerOpenSeconds,
    };
}
