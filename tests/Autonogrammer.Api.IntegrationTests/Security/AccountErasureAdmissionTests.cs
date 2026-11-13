using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.IntegrationTests.DataPlane;
using Autonogrammer.Cowork.Memory;
using Autonogrammer.Cowork.Memory.Postgres;
using Autonogrammer.Sessions.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>
/// The erasure admission fence seen through the API. An account is erased through <see cref="PostgresAccountErasure"/>, the path the erase-account verb uses,
/// on a run-owned migrated database. A write for that account is then answered 409 with the account-unavailable problem and creates no row, while another
/// account keeps writing. The write route is <c>POST /cowork/memory</c>, which admits through <see cref="AccountAdmission"/> before it inserts.
/// </summary>
public sealed class AccountErasureAdmissionTests
{
    private const string Alice = "alice";
    private const string Bob = "bob";
    private const string MemoryPath = "/cowork/memory";
    private const string SaveBody = "{\"content\":\"remember this\"}";
    private const string AccountUnavailableType = "/problems/account-unavailable";
    private const string MigratorKey = "Sessions:Postgres:MigratorConnectionString";
    private const string EntryCountSql = "SELECT count(*) FROM cowork_memory_entries WHERE account_id = @a";

    /// <summary>After erasure, an authenticated write for the erased account is a 409 account-unavailable problem that names neither the account nor SQL.</summary>
    [Fact]
    public async Task WriteAfterErasure_IsAccountUnavailableProblem_WithoutTheAccountIdOrSql()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await ErasureHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client(Alice);
        using var created = await SaveAsync(alice, ct).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        await host.EraseAsync(Alice, ct).ConfigureAwait(true);
        using var refused = await SaveAsync(alice, ct).ConfigureAwait(true);

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        refused.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");
        var body = await refused.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        using var problem = JsonDocument.Parse(body);
        var root = problem.RootElement;
        root.GetProperty("type").GetString().ShouldBe(AccountUnavailableType);
        root.GetProperty("title").GetString().ShouldBe("Account is being erased");
        root.GetProperty("detail").GetString().ShouldBe("The account is unavailable for changes.");
        body.ShouldNotContain(Alice, Case.Insensitive);
        body.ShouldNotContain("SQL", Case.Insensitive);
    }

    /// <summary>Erasing one account leaves another account's writes at their normal success status.</summary>
    [Fact]
    public async Task AnotherAccount_StillWritesAfterErasure_WithItsNormalStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await ErasureHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var bob = host.Client(Bob);

        await host.EraseAsync(Alice, ct).ConfigureAwait(true);
        using var saved = await SaveAsync(bob, ct).ConfigureAwait(true);

        saved.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.EntryCountAsync(Bob, ct).ConfigureAwait(true)).ShouldBe(1L, "the other account's write is stored");
    }

    /// <summary>A write refused by erasure leaves the erased account with no row: the row count stays where erasure left it.</summary>
    [Fact]
    public async Task RefusedWriteAfterErasure_CreatesNoRowForTheAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await ErasureHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client(Alice);
        using var created = await SaveAsync(alice, ct).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.EntryCountAsync(Alice, ct).ConfigureAwait(true)).ShouldBe(1L, "the write before erasure is stored");

        await host.EraseAsync(Alice, ct).ConfigureAwait(true);
        (await host.EntryCountAsync(Alice, ct).ConfigureAwait(true)).ShouldBe(0L, "erasure deletes the account's entries");
        using var refused = await SaveAsync(alice, ct).ConfigureAwait(true);

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await host.EntryCountAsync(Alice, ct).ConfigureAwait(true)).ShouldBe(0L, "the refused write inserts no row");
    }

    private static async Task<HttpResponseMessage> SaveAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var body = new StringContent(SaveBody, Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri(MemoryPath, UriKind.Relative), body, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The SHA-256 of the account as lowercase hexadecimal with a prefix: a digest that does not contain the account identifier, as the keyed hash does not.</summary>
    private static string HashOf(string account) => "hash-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(account)));

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

    /// <summary>A migrated run-owned database, a runtime data source that owns it, and a production-shaped host whose memory store writes to it.</summary>
    private sealed class ErasureHost : IAsyncDisposable
    {
        private readonly ThrowawayDatabase _database;
        private readonly NpgsqlDataSource _dataSource;
        private readonly PostgresAccountErasure _erasure;
        private readonly ApiFactory _factory;

        private ErasureHost(ThrowawayDatabase database, NpgsqlDataSource dataSource)
        {
            _database = database;
            _dataSource = dataSource;
            _erasure = new PostgresAccountErasure(dataSource);
            _factory = CreateFactory(database, dataSource);
        }

        public static async Task<ErasureHost> StartAsync(CancellationToken cancellationToken)
        {
            var database = await CreateMigratedDatabaseAsync(cancellationToken).ConfigureAwait(false);
            var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource();
            await AccountLifecycleProvisioning.ProvisionAsync(dataSource, [Alice, Bob], cancellationToken).ConfigureAwait(false);
            return new ErasureHost(database, dataSource);
        }

        public HttpClient Client(string account)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken(account));
            return client;
        }

        public Task EraseAsync(string account, CancellationToken cancellationToken) =>
            _erasure.EraseAsync(account, HashOf(account), static (_, _) => Task.CompletedTask, cancellationToken);

        public async Task<long> EntryCountAsync(string account, CancellationToken cancellationToken)
        {
            using var command = _dataSource.CreateCommand(EntryCountSql);
            command.Parameters.AddWithValue("a", account);
            return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }

        public async ValueTask DisposeAsync()
        {
            _factory.Dispose();
            await _dataSource.DisposeAsync().ConfigureAwait(false);
            await _database.DisposeAsync().ConfigureAwait(false);
        }

        private static ApiFactory CreateFactory(ThrowawayDatabase database, NpgsqlDataSource dataSource)
        {
            var config = JwtTestHost.ProductionConfig();
            config["Sessions:Postgres:ConnectionString"] = database.ConnectionString;
            config["Cowork:Projects:Enabled"] = "true";
            config["Cowork:Memory:Enabled"] = "true";
            return new ApiFactory(config, services =>
            {
                services.AddSingleton(dataSource);
                services.RemoveAll<IMemoryStore>();
                services.AddSingleton<IMemoryStore>(new PostgresMemoryStore(dataSource));
                JwtTestHost.InjectStaticMetadata(services);
            }, environment: "Production");
        }
    }
}
