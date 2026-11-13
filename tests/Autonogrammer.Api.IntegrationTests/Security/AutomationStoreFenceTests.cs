using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.IntegrationTests.DataPlane;
using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Sessions.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>
/// The automation store fence against a run-owned migrated database. The store is resolved from the API's own registration, so with
/// <c>Sessions:Store=Postgres</c> it is <see cref="Autonogrammer.Api.Automations.PostgresAutomationStore"/>. A write for an active owner is readable; a write for an
/// owner that erasure has erased is refused with <see cref="AccountErasingException"/> and leaves no row; and erasure removes the owner's definitions and run history
/// while another owner's automation and history survive.
/// </summary>
public sealed class AutomationStoreFenceTests
{
    private const string Alice = "alice";
    private const string Bob = "bob";
    private const string MigratorKey = "Sessions:Postgres:MigratorConnectionString";
    private const string AutomationRowsSql = "SELECT count(*) FROM automations WHERE owner_id = @a";
    private const string RunRowsSql = "SELECT count(*) FROM automation_runs WHERE owner_id = @a";
    private const string AliceDaily = "alice-daily";
    private const string AliceLate = "alice-late";
    private const string BobDaily = "bob-daily";
    private const string AliceRun = "alice-run-1";
    private const string BobRun = "bob-run-1";

    private static readonly DateTimeOffset Start = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The control: an automation saved for an active owner is readable through the store the API selects.</summary>
    [Fact]
    public async Task ActiveOwner_SavedAutomationIsReadable()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await CreateMigratedDatabaseAsync(ct).ConfigureAwait(true);
        await using var databaseScope = database.ConfigureAwait(true);
        var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource();
        await using var dataSourceScope = dataSource.ConfigureAwait(true);
        await AccountLifecycleProvisioning.ProvisionAsync(dataSource, [Alice, Bob], ct).ConfigureAwait(true);
        using var factory = CreateFactory(database, dataSource);
        var store = factory.Services.GetRequiredService<IAutomationStore>();

        await store.SaveAsync(Automation(AliceDaily, Alice), ct).ConfigureAwait(true);

        var read = await store.GetAsync(AliceDaily, ct).ConfigureAwait(true);
        read.ShouldNotBeNull("an automation saved for an active owner is stored");
        read.Owner.ShouldBe(Alice);
        (await CountAsync(dataSource, AutomationRowsSql, Alice, ct).ConfigureAwait(true)).ShouldBe(1L, "the saved automation has one row");
    }

    /// <summary>After erasure, a save for the erased owner is refused and inserts no row; the erased owner's earlier automation stays gone.</summary>
    [Fact]
    public async Task ErasedOwner_SaveIsRefusedAndLeavesNoRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await CreateMigratedDatabaseAsync(ct).ConfigureAwait(true);
        await using var databaseScope = database.ConfigureAwait(true);
        var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource();
        await using var dataSourceScope = dataSource.ConfigureAwait(true);
        await AccountLifecycleProvisioning.ProvisionAsync(dataSource, [Alice, Bob], ct).ConfigureAwait(true);
        using var factory = CreateFactory(database, dataSource);
        var store = factory.Services.GetRequiredService<IAutomationStore>();
        await store.SaveAsync(Automation(AliceDaily, Alice), ct).ConfigureAwait(true);

        await EraseAsync(dataSource, Alice, ct).ConfigureAwait(true);
        _ = await Should.ThrowAsync<AccountErasingException>(() => store.SaveAsync(Automation(AliceLate, Alice), ct).AsTask()).ConfigureAwait(true);

        (await CountAsync(dataSource, AutomationRowsSql, Alice, ct).ConfigureAwait(true)).ShouldBe(0L, "the refused save inserts no row for the erased owner");
        (await store.GetAsync(AliceLate, ct).ConfigureAwait(true)).ShouldBeNull("the refused automation was not stored");
    }

    /// <summary>Erasure removes the erased owner's definitions and run history; another owner's automation and its history survive.</summary>
    [Fact]
    public async Task Erasure_RemovesOwnerDefinitionsAndRuns_AndKeepsAnotherOwnersAutomation()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await CreateMigratedDatabaseAsync(ct).ConfigureAwait(true);
        await using var databaseScope = database.ConfigureAwait(true);
        var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource();
        await using var dataSourceScope = dataSource.ConfigureAwait(true);
        await AccountLifecycleProvisioning.ProvisionAsync(dataSource, [Alice, Bob], ct).ConfigureAwait(true);
        using var factory = CreateFactory(database, dataSource);
        var store = factory.Services.GetRequiredService<IAutomationStore>();
        await store.SaveAsync(Automation(AliceDaily, Alice), ct).ConfigureAwait(true);
        await store.UpsertRunAsync(AliceDaily, Run(AliceRun), ct).ConfigureAwait(true);
        await store.SaveAsync(Automation(BobDaily, Bob), ct).ConfigureAwait(true);
        await store.UpsertRunAsync(BobDaily, Run(BobRun), ct).ConfigureAwait(true);
        (await CountAsync(dataSource, RunRowsSql, Alice, ct).ConfigureAwait(true)).ShouldBe(1L, "the erased owner's run is stored before erasure");

        await EraseAsync(dataSource, Alice, ct).ConfigureAwait(true);

        (await CountAsync(dataSource, AutomationRowsSql, Alice, ct).ConfigureAwait(true)).ShouldBe(0L, "erasure deletes the owner's automation definitions");
        (await CountAsync(dataSource, RunRowsSql, Alice, ct).ConfigureAwait(true)).ShouldBe(0L, "erasure deletes the owner's run history");
        (await store.GetAsync(AliceDaily, ct).ConfigureAwait(true)).ShouldBeNull("the erased owner's automation is gone");
        var survivor = await store.GetAsync(BobDaily, ct).ConfigureAwait(true);
        survivor.ShouldNotBeNull("another owner's automation survives erasure");
        survivor.Owner.ShouldBe(Bob);
        (await store.GetHistoryAsync(BobDaily, ct).ConfigureAwait(true)).Count.ShouldBe(1, "another owner's run history survives erasure");
    }

    private static UserAutomation Automation(string id, string owner) => new()
    {
        Id = id,
        Owner = owner,
        Name = "Daily digest",
        Prompt = "Summarise the day.",
        Enabled = true,
        CreatedAt = Start,
        UpdatedAt = Start,
    };

    private static AutomationRunEntry Run(string runId) => new()
    {
        RunId = runId,
        Source = AutomationRunSource.Manual,
        Status = AutomationRunStatus.Succeeded,
        StartedAt = Start,
        FinishedAt = Start.AddSeconds(5),
    };

    private static Task EraseAsync(NpgsqlDataSource dataSource, string account, CancellationToken cancellationToken) =>
        new PostgresAccountErasure(dataSource).EraseAsync(account, "hash-" + account, static (_, _) => Task.CompletedTask, cancellationToken);

    private static async Task<long> CountAsync(NpgsqlDataSource dataSource, string sql, string owner, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("a", owner);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>The API host whose session store is PostgreSQL on the run-owned database, so <see cref="IAutomationStore"/> resolves to the PostgreSQL store.</summary>
    private static ApiFactory CreateFactory(ThrowawayDatabase database, NpgsqlDataSource dataSource)
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Sessions:Store"] = "Postgres",
            ["Sessions:Postgres:ConnectionString"] = database.ConnectionString,
        };
        return new ApiFactory(config, services => services.AddSingleton(dataSource));
    }

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
}
