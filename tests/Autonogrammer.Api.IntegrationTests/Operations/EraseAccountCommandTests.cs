using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.IntegrationTests.DataPlane;
using Autonogrammer.Api.Operations;
using Autonogrammer.Sessions.Postgres;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Operations;

/// <summary>
/// The <c>erase-account</c> verb against a run-owned database. Without the operator token the verb refuses with exit code 3 and erases nothing; with the matching
/// token it erases the account's rows and marks the account's lifecycle row <c>erased</c>. The verb reads its settings from the process environment, so the class
/// runs in the collection that runs alone (<c>ProcessPathSerialization</c>).
/// </summary>
[Collection(ProcessPathSerialization.Name)]
public sealed class EraseAccountCommandTests
{
    private const string ConnectionStringVariable = "Sessions__Postgres__ConnectionString";
    private const string CoordinationStoreVariable = "Coordination__Store";
    private const string InMemoryStore = "InMemory";
    private const string MigratorKey = "Sessions:Postgres:MigratorConnectionString";
    private const string ActiveState = "active";
    private const string ErasedState = "erased";

    private static readonly long[] OneRowEach = [1L, 1L, 1L, 1L];
    private static readonly long[] NoRows = [0L, 0L, 0L, 0L];

    private const string SeedSql = """
        INSERT INTO account_lifecycle (account_id) VALUES (@a) ON CONFLICT (account_id) DO NOTHING;
        INSERT INTO accounts (id, created_at, last_seen_at) VALUES (@a, now(), now());
        INSERT INTO dispatch_devices (id, account_id, name, platform, public_key, created_at) VALUES (@a || '-dev', @a, 'laptop', 'linux', 'key', now());
        INSERT INTO cowork_instructions (account_id, body, updated_at) VALUES (@a, 'be brief', now());
        INSERT INTO sessions (id, owner_id, created_at, updated_at, status, messages) VALUES (@a || '-s1', @a, now(), now(), 'Idle', '[]');
        """;

    private const string OwnedRowsSql = """
        SELECT
            (SELECT count(*) FROM accounts WHERE id = @a),
            (SELECT count(*) FROM dispatch_devices WHERE account_id = @a),
            (SELECT count(*) FROM cowork_instructions WHERE account_id = @a),
            (SELECT count(*) FROM sessions WHERE owner_id = @a)
        """;

    private const string LifecycleStateSql = "SELECT state FROM account_lifecycle WHERE account_id = @a";

    /// <summary>Verifies the verb refuses without the operator token, exits with code 3 and erases nothing.</summary>
    [Fact]
    public async Task WithoutTheOperatorToken_TheVerbRefusesAndErasesNothing()
    {
        var testToken = TestContext.Current.CancellationToken;
        var database = await CreateMigratedDatabaseAsync(testToken).ConfigureAwait(true);
        await using (database.ConfigureAwait(true))
        {
            using var environment = new EnvironmentScope(database.ConnectionString, DigestOf("operator-token-" + Guid.NewGuid().ToString("N")), token: null);
            var account = NewAccountId();
            var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource();
            await using (dataSource.ConfigureAwait(true))
            {
                await SeedAccountAsync(dataSource, account, testToken).ConfigureAwait(true);
                (await OwnedRowsAsync(dataSource, account, testToken).ConfigureAwait(true)).ShouldBe(OneRowEach);

                var result = await RunVerbAsync(account, testToken).ConfigureAwait(true);

                result.Code.ShouldBe(EraseAccountCommand.Refused);
                result.Error.ShouldContain(EraseAccountCommand.TokenEnvironmentVariable);
                result.Output.ShouldBeEmpty();
                (await OwnedRowsAsync(dataSource, account, testToken).ConfigureAwait(true)).ShouldBe(OneRowEach);
                (await LifecycleStateAsync(dataSource, account, testToken).ConfigureAwait(true)).ShouldBe(ActiveState);
            }
        }
    }

    /// <summary>Verifies the verb with the matching operator token erases the account's rows and marks its lifecycle row erased.</summary>
    [Fact]
    public async Task WithTheMatchingOperatorToken_TheAccountIsErased()
    {
        var testToken = TestContext.Current.CancellationToken;
        var database = await CreateMigratedDatabaseAsync(testToken).ConfigureAwait(true);
        await using (database.ConfigureAwait(true))
        {
            var operatorToken = "operator-token-" + Guid.NewGuid().ToString("N");
            using var environment = new EnvironmentScope(database.ConnectionString, DigestOf(operatorToken), operatorToken);
            var account = NewAccountId();
            var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource();
            await using (dataSource.ConfigureAwait(true))
            {
                await SeedAccountAsync(dataSource, account, testToken).ConfigureAwait(true);
                (await OwnedRowsAsync(dataSource, account, testToken).ConfigureAwait(true)).ShouldBe(OneRowEach);

                var result = await RunVerbAsync(account, testToken).ConfigureAwait(true);

                result.Code.ShouldBe(EraseAccountCommand.Succeeded, result.Error);
                result.Output.ShouldContain("Erased the account");
                (result.Output + result.Error).ShouldNotContain(operatorToken);
                (await OwnedRowsAsync(dataSource, account, testToken).ConfigureAwait(true)).ShouldBe(NoRows);
                (await LifecycleStateAsync(dataSource, account, testToken).ConfigureAwait(true)).ShouldBe(ErasedState);
            }
        }
    }

    /// <summary>Creates a throwaway database and migrates it to the compiled head, so the verb runs against a schema it can erase.</summary>
    /// <param name="cancellationToken">Cancels the creation and the migration.</param>
    /// <returns>The migrated database. The caller disposes it.</returns>
    private static async Task<ThrowawayDatabase> CreateMigratedDatabaseAsync(CancellationToken cancellationToken)
    {
        var database = await ThrowawayDatabase.CreateAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            string[] migrate = ["migrate", $"--{MigratorKey}={database.ConnectionString}"];
            var code = await MigrateCommand.RunAsync(migrate, output, error, cancellationToken).ConfigureAwait(false);
            code.ShouldBe(MigrateCommand.Succeeded);
            return database;
        }
        catch
        {
            await database.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<(int Code, string Output, string Error)> RunVerbAsync(string account, CancellationToken cancellationToken)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await EraseAccountCommand.RunAsync([EraseAccountCommand.Verb, account], output, error, cancellationToken).ConfigureAwait(false);
        return (code, output.ToString(), error.ToString());
    }

    private static string NewAccountId() => "acct-erase-" + Guid.NewGuid().ToString("N");

    /// <summary>The SHA-256 of <paramref name="token"/> as lowercase hexadecimal, the form the operator digest is configured in.</summary>
    private static string DigestOf(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static async Task SeedAccountAsync(NpgsqlDataSource dataSource, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(SeedSql);
        command.Parameters.AddWithValue("a", account);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Counts the account's rows in the accounts, dispatch devices, cowork instructions and sessions families.</summary>
    private static async Task<long[]> OwnedRowsAsync(NpgsqlDataSource dataSource, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(OwnedRowsSql);
        command.Parameters.AddWithValue("a", account);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var counts = new long[reader.FieldCount];
        for (var index = 0; index < counts.Length; index++)
        {
            counts[index] = reader.GetInt64(index);
        }

        return counts;
    }

    private static async Task<string?> LifecycleStateAsync(NpgsqlDataSource dataSource, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(LifecycleStateSql);
        command.Parameters.AddWithValue("a", account);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    /// <summary>Sets the verb's process environment and restores it on disposal. The variables are process-wide, hence the collection.</summary>
    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

        public EnvironmentScope(string connectionString, string digest, string? token)
        {
            Set(ConnectionStringVariable, connectionString);
            Set(CoordinationStoreVariable, InMemoryStore);
            Set(EraseAccountCommand.TokenSha256EnvironmentVariable, digest);
            Set(EraseAccountCommand.TokenEnvironmentVariable, token);
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        private void Set(string name, string? value)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
