#pragma warning disable CA2007, CA2100, MA0004, IDE0005, CS8604
using System.Text;
using Autonogrammer.Cowork.Memory;
using Autonogrammer.Cowork.Memory.Postgres;
using Autonogrammer.Cowork.Memory.Tests;
using Autonogrammer.Sessions.Postgres.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Memory.Postgres.Tests;

/// <summary>Two-account isolation, cascade delete, the GIN plan and the log canary.</summary>
[Trait("Category", "Live")]
public sealed class PostgresMemoryStoreTests : MemoryStoreContractTests, IAsyncLifetime
{
    private const string Canary = "memory-canary-7f3a91c0";
    private const string InstructionCanary = "instruction-canary-91bc02e4";

    private readonly string _schema = "t_" + Guid.NewGuid().ToString("n");
    private NpgsqlDataSource? _admin;
    private NpgsqlDataSource? _app;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION is unset. Source scripts/test-services.sh up before these tests.");
        }

        _admin = NpgsqlDataSource.Create(configured);
        await new MigrationRunner(_admin, _schema).MigrateAsync(CancellationToken.None).ConfigureAwait(true);
        var builder = new NpgsqlConnectionStringBuilder(configured) { SearchPath = _schema + ",public" };
        _app = NpgsqlDataSource.Create(builder.ConnectionString);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(true);
        }

        if (_admin is null)
        {
            return;
        }

        await using var connection = await _admin.OpenConnectionAsync(CancellationToken.None).ConfigureAwait(true);
        await using var drop = new NpgsqlCommand("DROP SCHEMA IF EXISTS " + _schema + " CASCADE", connection);
        _ = await drop.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(true);
        await _admin.DisposeAsync().ConfigureAwait(true);
    }

    /// <inheritdoc />
    protected override IMemoryStore Create(string accountId, IMemoryNoticeSink notices)
    {
        _ = accountId;
        return new PostgresMemoryStore(_app!, notices: notices);
    }

    /// <inheritdoc />
    protected override async Task EnsureProjectAsync(string accountId, string projectId)
    {
        await using var command = _app!.CreateCommand(
            """
            INSERT INTO cowork_projects (id, account_id, name, created_at, updated_at)
            VALUES (@id, @account_id, @name, now(), now())
            ON CONFLICT (id) DO NOTHING
            """);
        command.Parameters.AddWithValue("id", projectId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("name", "p");
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Deleting a project cascades its memory and leaves the other account untouched.</summary>
    [Fact]
    public async Task CascadeDeleteRemovesProjectMemoryOnly()
    {
        var store = new PostgresMemoryStore(_app!);
        await EnsureProjectAsync("left", "proj").ConfigureAwait(true);
        await EnsureProjectAsync("right", "other").ConfigureAwait(true);
        var token = TestContext.Current.CancellationToken;
        _ = await store.SaveAsync(new MemoryScope("left", "proj", false, "sess"), new MemoryDraft("tea", Canary, null), token).ConfigureAwait(true);
        _ = await store.SaveAsync(new MemoryScope("right", "other", false, "sess"), new MemoryDraft("tea", "right stays", null), token).ConfigureAwait(true);
        await using var delete = _app!.CreateCommand("DELETE FROM cowork_projects WHERE id = @id AND account_id = @account_id");
        delete.Parameters.AddWithValue("id", "proj");
        delete.Parameters.AddWithValue("account_id", "left");
        _ = await delete.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        (await store.RecallAsync(new MemoryScope("left", "proj", false, "sess"), null, 5, token).ConfigureAwait(true)).Entries.ShouldBeEmpty();
        (await store.RecallAsync(new MemoryScope("right", "other", false, "sess"), null, 5, token).ConfigureAwait(true)).Entries.Single().Content.ShouldBe("right stays");
    }

    /// <summary>EXPLAIN uses the M0009 GIN index for the search expression.</summary>
    [Fact]
    public async Task ExplainUsesTheSearchIndex()
    {
        PostgresMemoryStore.SearchVectorExpression.ShouldBe("to_tsvector('simple', coalesce(topic, '') || ' ' || coalesce(content, ''))");
        var store = new PostgresMemoryStore(_app!);
        var token = TestContext.Current.CancellationToken;
        _ = await store.SaveAsync(new MemoryScope("acct", null, false, "sess"), new MemoryDraft("tea", "oolong leaves", null), token).ConfigureAwait(true);
        var sql = $"""
            EXPLAIN (FORMAT TEXT)
            SELECT id FROM cowork_memory_entries
            WHERE {PostgresMemoryStore.SearchVectorExpression} @@ plainto_tsquery('simple', @query)
            """;
        await using var connection = await _app!.OpenConnectionAsync(token).ConfigureAwait(true);
        await using (var off = new NpgsqlCommand("SET enable_seqscan = off", connection))
        {
            _ = await off.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("query", "oolong");
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(true);
        var plan = new StringBuilder();
        while (await reader.ReadAsync(token).ConfigureAwait(true))
        {
            _ = plan.AppendLine(reader.GetString(0));
        }

        plan.ToString().ShouldContain(PostgresMemoryStore.SearchIndexName);
    }

    /// <summary>Memory and instruction text never appear in ToString, a refusal sentence, or an exception message.</summary>
    [Fact]
    public async Task LogCanaryStaysOutOfExceptionsAndToString()
    {
        var store = new PostgresMemoryStore(_app!);
        var token = TestContext.Current.CancellationToken;
        var saved = await store.SaveAsync(new MemoryScope("acct", null, false, "sess"), new MemoryDraft(InstructionCanary, Canary, null), token).ConfigureAwait(true);
        saved.Entry.ShouldNotBeNull();
        saved.Entry.ToString().ShouldNotContain(Canary);
        saved.Entry.ToString().ShouldNotContain(InstructionCanary);
        var refused = await store.SaveAsync(new MemoryScope("acct", null, false, "sess"), new MemoryDraft("id", "SSN 219-09-9999 " + Canary, null), token).ConfigureAwait(true);
        refused.Sentence.ShouldNotBeNull();
        refused.Sentence.ShouldNotContain(Canary);
        refused.Sentence.ShouldNotContain(InstructionCanary);
        var thrown = await Should.ThrowAsync<ArgumentException>(() => new PostgresMemoryStore(_app!).GetSettingsAsync("  ", token)).ConfigureAwait(true);
        thrown.ToString().ShouldNotContain(Canary);
        thrown.ToString().ShouldNotContain(InstructionCanary);
    }
}
