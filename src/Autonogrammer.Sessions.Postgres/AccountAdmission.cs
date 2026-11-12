using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// The admission every account-data writer takes inside its own transaction. It serialises writers against account erasure: erasure takes the lifecycle row
/// exclusively, so a writer that holds the shared lock through commit cannot interleave with it. A writer for an account with no lifecycle row is refused,
/// because nothing can fence it. Rows are created when the account is provisioned, through <see cref="EnsureRowAsync"/>, never inside a writer transaction.
/// </summary>
public static class AccountAdmission
{
    private const string EnsureRowSql =
        "INSERT INTO account_lifecycle (account_id) VALUES (@account_id) ON CONFLICT (account_id) DO NOTHING";

    private const string LockSharedSql = "SELECT state FROM account_lifecycle WHERE account_id = @account_id FOR SHARE";

    /// <summary>
    /// Creates the lifecycle row for an account if it does not exist. Call it where the account is provisioned, outside any writer transaction, so writers
    /// never wait on an uncommitted insert.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="accountId">The account being provisioned.</param>
    /// <param name="cancellationToken">Cancels the database round trip.</param>
    public static async Task EnsureRowAsync(NpgsqlConnection connection, string accountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        using var ensure = new NpgsqlCommand(EnsureRowSql, connection);
        ensure.Parameters.AddWithValue("account_id", accountId);
        await ensure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the shared admission for <paramref name="accountId"/> inside <paramref name="transaction"/>. The lock is held until that transaction ends.
    /// </summary>
    /// <param name="connection">The connection the transaction belongs to.</param>
    /// <param name="transaction">The caller's open transaction; the lock is released when it commits or rolls back.</param>
    /// <param name="accountId">The account the write belongs to. Passed as a parameter, never interpolated into SQL.</param>
    /// <param name="cancellationToken">Cancels the database round trip.</param>
    /// <exception cref="AccountErasingException">The account is erasing, erased, or has no lifecycle row (not provisioned).</exception>
    public static async Task AdmitSharedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string accountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        using var lockRow = new NpgsqlCommand(LockSharedSql, connection, transaction);
        lockRow.Parameters.AddWithValue("account_id", accountId);
        var state = await lockRow.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (state is null)
        {
            throw new AccountErasingException($"Account '{accountId}' is not provisioned; the write was refused.");
        }

        if (state is "erasing" or "erased")
        {
            throw new AccountErasingException($"Account '{accountId}' is being erased; the write was refused.");
        }
    }
}
