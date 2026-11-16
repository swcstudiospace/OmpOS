using Autonogrammer.Resilience;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// Recognises a refusal of the account erasure fence (migration M0012). The stores that share the <see cref="PostgresResilience"/> pipeline raise it as an
/// <see cref="AccountErasingException"/>; a store that runs its own commands (memory, projects, artifacts, accounts) lets the driver's <see cref="PostgresException"/> through,
/// and this class tells both apart from every other failure so a caller can fail closed and not retry.
/// </summary>
public static class AccountErasureFence
{
    /// <summary>SQLSTATE of a write for an account that is being erased or has been erased.</summary>
    public const string ErasedSqlState = "AE001";

    /// <summary>SQLSTATE of a write whose owning account cannot be resolved (for example a session row that is already gone).</summary>
    public const string OwnerUnresolvedSqlState = "AE002";

    /// <summary>SQLSTATE of a write in a transaction that does not run at READ COMMITTED, where the fence could not be seen reliably.</summary>
    public const string IsolationSqlState = "AE003";

    private const int MaxChainDepth = 8;

    /// <summary>Says whether a failure, or anything it wraps, is a refusal of the fence.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns><see langword="true"/> for an <see cref="AccountErasingException"/> or a <see cref="PostgresException"/> with one of the fence SQLSTATEs.</returns>
    public static bool IsRefusal(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var current = exception;
        for (var depth = 0; current is not null && depth < MaxChainDepth; depth++, current = current.InnerException)
        {
            if (current is AccountErasingException or PostgresException { SqlState: ErasedSqlState or OwnerUnresolvedSqlState or IsolationSqlState })
            {
                return true;
            }
        }

        return false;
    }
}
