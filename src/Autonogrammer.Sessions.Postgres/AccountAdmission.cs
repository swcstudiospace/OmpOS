using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// The shared admission every account-data writer takes inside its own transaction. It serialises writers against account erasure: erasure takes the row
/// exclusively, so a writer that holds the shared lock through commit cannot interleave with it, and a writer that arrives after erasure begins is refused.
/// </summary>
public static class AccountAdmission
{
    private const string EnsureRowSql =
        "INSERT INTO account_lifecycle (account_id) VALUES (@account_id) ON CONFLICT (account_id) DO NOTHING";

    private const string LockSharedSql = "SELECT state FROM account_lifecycle WHERE account_id = @account_id FOR SHARE";

    /// <summary>
    /// Takes the shared admission for <paramref name="accountId"/> inside <paramref name="transaction"/>. The lock is held until that transaction ends.
    /// </summary>
    /// <param name="connection">The connection the transaction belongs to.</param>
    /// <param name="transaction">The caller's open transaction; the lock is released when it commits or rolls back.</param>
    /// <param name="accountId">The account the write belongs to. Passed as a parameter, never interpolated into SQL.</param>
    /// <param name="cancellationToken">Cancels the database round trips.</param>
    /// <exception cref="AccountErasingException">The account is in state <c>erasing</c> or <c>erased</c>.</exception>
    public static async Task AdmitSharedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string accountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        using (var ensure = new NpgsqlCommand(EnsureRowSql, connection, transaction))
        {
            ensure.Parameters.AddWithValue("account_id", accountId);
            await ensure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using var lockRow = new NpgsqlCommand(LockSharedSql, connection, transaction);
        lockRow.Parameters.AddWithValue("account_id", accountId);
        var state = await lockRow.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (state is "erasing" or "erased")
        {
            throw new AccountErasingException($"Account '{accountId}' is being erased; the write was refused.");
        }
    }
}
