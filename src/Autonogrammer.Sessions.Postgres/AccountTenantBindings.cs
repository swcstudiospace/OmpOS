using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// The binding from an account identifier to its keyed tenant hash, so a writer that holds only the tenant hash (the ledger outbox) can resolve the account whose
/// <see cref="AccountAdmission"/> fences the write. Bindings are created when the account is provisioned and removed by <see cref="PostgresAccountErasure"/>
/// inside its lifecycle transaction. Every statement takes its values as parameters.
/// </summary>
public static class AccountTenantBindings
{
    private const string InsertSql =
        "INSERT INTO account_tenant_bindings (account_id, tenant_hash) VALUES (@account_id, @tenant_hash) ON CONFLICT (account_id) DO NOTHING";

    private const string SelectTenantHashSql = "SELECT tenant_hash FROM account_tenant_bindings WHERE account_id = @account_id";

    private const string SelectAccountIdSql = "SELECT account_id FROM account_tenant_bindings WHERE tenant_hash = @tenant_hash";

    private const string DeleteSql = "DELETE FROM account_tenant_bindings WHERE account_id = @account_id";

    /// <summary>
    /// Creates the binding of <paramref name="accountId"/> to <paramref name="tenantHash"/>, or confirms that it already exists. Call it where the account is
    /// provisioned, inside the caller's transaction.
    /// </summary>
    /// <param name="connection">The connection the transaction belongs to.</param>
    /// <param name="transaction">The caller's open transaction.</param>
    /// <param name="accountId">The account being bound. Passed as a parameter, never interpolated into SQL.</param>
    /// <param name="tenantHash">The keyed tenant hash of the account. Passed as a parameter, never interpolated into SQL.</param>
    /// <param name="cancellationToken">Cancels the database round trips.</param>
    /// <exception cref="InvalidOperationException">The account is already bound to a different tenant hash.</exception>
    /// <exception cref="PostgresException">The tenant hash is already bound to a different account (unique violation).</exception>
    public static async Task EnsureAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string accountId,
        string tenantHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantHash);

        using (var insert = PostgresSql.Create(connection, transaction))
        {
            insert.CommandText = InsertSql;
            insert.Parameters.AddWithValue("account_id", accountId);
            insert.Parameters.AddWithValue("tenant_hash", tenantHash);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using var select = PostgresSql.Create(connection, transaction);
        select.CommandText = SelectTenantHashSql;
        select.Parameters.AddWithValue("account_id", accountId);
        var bound = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (!string.Equals(bound, tenantHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Account '{accountId}' is already bound to a different tenant; the binding was not changed.");
        }
    }

    /// <summary>
    /// Resolves a keyed tenant hash to the account it was bound to.
    /// </summary>
    /// <param name="connection">The connection the transaction belongs to.</param>
    /// <param name="transaction">The caller's open transaction.</param>
    /// <param name="tenantHash">The keyed tenant hash to resolve. Passed as a parameter, never interpolated into SQL.</param>
    /// <param name="cancellationToken">Cancels the database round trip.</param>
    /// <returns>The account identifier, or <see langword="null"/> when no account is bound to the tenant hash.</returns>
    public static async Task<string?> ResolveAccountIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tenantHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantHash);

        using var select = PostgresSql.Create(connection, transaction);
        select.CommandText = SelectAccountIdSql;
        select.Parameters.AddWithValue("tenant_hash", tenantHash);
        return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    /// <summary>
    /// Removes the binding of <paramref name="accountId"/>. Idempotent: deleting an unbound account changes nothing.
    /// </summary>
    /// <param name="connection">The connection the transaction belongs to.</param>
    /// <param name="transaction">The caller's open transaction; the delete commits or rolls back with it.</param>
    /// <param name="accountId">The account whose binding is removed. Passed as a parameter, never interpolated into SQL.</param>
    /// <param name="cancellationToken">Cancels the database round trip.</param>
    public static async Task DeleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string accountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        using var delete = PostgresSql.Create(connection, transaction);
        delete.CommandText = DeleteSql;
        delete.Parameters.AddWithValue("account_id", accountId);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
