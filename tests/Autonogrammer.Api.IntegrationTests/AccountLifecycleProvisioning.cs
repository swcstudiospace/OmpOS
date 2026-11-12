using Npgsql;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Provisions account lifecycle rows for fixtures that write account-keyed rows to PostgreSQL. The admission fence refuses a writer for an account with no
/// lifecycle row, and rows are created when an account is provisioned, so each fixture provisions its accounts before their first write.
/// </summary>
internal static class AccountLifecycleProvisioning
{
    private const string ProvisionSql = "INSERT INTO account_lifecycle (account_id) VALUES (@account_id) ON CONFLICT (account_id) DO NOTHING";

    /// <summary>Creates the lifecycle row of each account that does not have one yet.</summary>
    /// <param name="source">The data source whose search path names the schema that holds account_lifecycle.</param>
    /// <param name="accountIds">The accounts to provision.</param>
    /// <param name="cancellationToken">Cancels the database round trips.</param>
    public static async Task ProvisionAsync(NpgsqlDataSource source, string[] accountIds, CancellationToken cancellationToken)
    {
        foreach (var accountId in accountIds)
        {
            var command = source.CreateCommand(ProvisionSql);
            await using var commandAsyncDisposal = command.ConfigureAwait(true);
            command.Parameters.AddWithValue("account_id", accountId);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
        }
    }
}
