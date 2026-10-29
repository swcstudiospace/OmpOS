using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// Erasing an account deletes its rows from every user-content family of the data plane (including the four memory tables and the artifact tables), runs the
/// tenant-key delegate once after the last commit, leaves every other account untouched, and leaves only the keyed-hash audit rows (AG-68-14). Every case uses a
/// fresh migrated schema on the shared server.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresAccountErasureTests(PostgresServerFixture server)
{
    private const string SeedSql = """
        INSERT INTO accounts (id, created_at, last_seen_at) VALUES (@a, now(), now());
        INSERT INTO dispatch_devices (id, account_id, name, platform, public_key, created_at) VALUES (@a || '-dev', @a, 'laptop', 'linux', 'key', now());
        INSERT INTO dispatch_commands (id, device_id, account_id, seq, op, state, idempotency_key, expires_at, created_at, updated_at)
            VALUES (@a || '-cmd', @a || '-dev', @a, 1, 'op', 'queued', 'key-1', now(), now(), now());
        INSERT INTO dispatch_events (account_id, id, occurred_at, kind) VALUES (@a, 1, now(), 'device.paired');
        INSERT INTO dispatch_audit (account_id_hash, occurred_at, op, outcome) VALUES (@h, now(), 'pair', 'ok');
        INSERT INTO ledger_outbox (audit_id, payload_digest, next_attempt_at) VALUES (1, 'digest-' || @h, now());
        INSERT INTO cowork_events (account_id, session_id, seq, occurred_at, kind, payload) VALUES (@a, @a || '-cs', 1, now(), 'artifact.created', '{}');
        INSERT INTO cowork_projects (id, account_id, name, created_at, updated_at) VALUES (@a || '-proj', @a, 'project', now(), now());
        INSERT INTO cowork_instructions (account_id, body, updated_at) VALUES (@a, 'be brief', now());
        INSERT INTO cowork_memory_entries (id, account_id, project_id, topic, content, created_at, updated_at)
            VALUES (@a || '-mem', @a, @a || '-proj', 'topic', 'content', now(), now());
        INSERT INTO cowork_memory_settings (account_id, enabled) VALUES (@a, true);
        INSERT INTO artifacts (id, account_id, title, kind, latest_number, created_at, updated_at) VALUES (@a || '-art', @a, 'title', 'doc', 1, now(), now());
        INSERT INTO artifact_versions (artifact_id, version, session_id, account_id, content_sha256, size_bytes, media_type, author, created_at, content)
            VALUES (@a || '-art', 1, @a || '-cs', @a, 'sha', 3, 'text/plain', 'user', now(), '\x010203');
        INSERT INTO artifact_grants (artifact_id, version, account_id, content_sha256, origins, scope, device, granted_at)
            VALUES (@a || '-art', 1, @a, 'sha', '[]', 'read', 'dev', now());
        INSERT INTO sessions (id, owner_id, created_at, updated_at, status, messages) VALUES (@a || '-s1', @a, now(), now(), 'Idle', '[]');
        INSERT INTO session_events (session_id, seq, correlation_id, occurred_at, payload) VALUES (@a || '-s1', 1, 'c', now(), '{}');
        INSERT INTO session_tool_approvals (session_id, request_id, tool_name, granted_tools, requested_at) VALUES (@a || '-s1', 'r1', 'Bash', '[]', now());
        INSERT INTO cloud_agents (id, owner_id, session_id, status, created_at, updated_at, record) VALUES (@a || '-agent', @a, @a || '-s1', 'Active', now(), now(), '{}');
        """;

    private const string CountsSql = """
        SELECT
            (SELECT count(*) FROM accounts WHERE id = @a) AS accounts,
            (SELECT count(*) FROM dispatch_devices WHERE account_id = @a) AS dispatch_devices,
            (SELECT count(*) FROM dispatch_commands WHERE account_id = @a) AS dispatch_commands,
            (SELECT count(*) FROM dispatch_events WHERE account_id = @a) AS dispatch_events,
            (SELECT count(*) FROM cowork_events WHERE account_id = @a) AS cowork_events,
            (SELECT count(*) FROM cowork_projects WHERE account_id = @a) AS cowork_projects,
            (SELECT count(*) FROM cowork_instructions WHERE account_id = @a) AS cowork_instructions,
            (SELECT count(*) FROM cowork_memory_entries WHERE account_id = @a) AS cowork_memory_entries,
            (SELECT count(*) FROM cowork_memory_settings WHERE account_id = @a) AS cowork_memory_settings,
            (SELECT count(*) FROM artifacts WHERE account_id = @a) AS artifacts,
            (SELECT count(*) FROM artifact_versions WHERE account_id = @a) AS artifact_versions,
            (SELECT count(*) FROM artifact_grants WHERE account_id = @a) AS artifact_grants,
            (SELECT count(*) FROM sessions WHERE owner_id = @a) AS sessions,
            (SELECT count(*) FROM session_events WHERE session_id = @a || '-s1') AS session_events,
            (SELECT count(*) FROM session_tool_approvals WHERE session_id = @a || '-s1') AS session_tool_approvals,
            (SELECT count(*) FROM cloud_agents WHERE owner_id = @a) AS cloud_agents
        """;

    private const string AuditCountSql = "SELECT count(*) FROM dispatch_audit WHERE account_id_hash = @h";

    private const string TextScanSql = """
        SELECT count(*) FROM (
            SELECT t::text AS row_text FROM accounts t
            UNION ALL SELECT t::text FROM dispatch_devices t
            UNION ALL SELECT t::text FROM dispatch_commands t
            UNION ALL SELECT t::text FROM dispatch_events t
            UNION ALL SELECT t::text FROM dispatch_audit t
            UNION ALL SELECT t::text FROM ledger_outbox t
            UNION ALL SELECT t::text FROM cowork_events t
            UNION ALL SELECT t::text FROM cowork_projects t
            UNION ALL SELECT t::text FROM cowork_instructions t
            UNION ALL SELECT t::text FROM cowork_memory_entries t
            UNION ALL SELECT t::text FROM cowork_memory_settings t
            UNION ALL SELECT t::text FROM artifacts t
            UNION ALL SELECT t::text FROM artifact_versions t
            UNION ALL SELECT t::text FROM artifact_grants t
            UNION ALL SELECT t::text FROM sessions t
            UNION ALL SELECT t::text FROM session_events t
            UNION ALL SELECT t::text FROM session_tool_approvals t
            UNION ALL SELECT t::text FROM cloud_agents t
        ) scanned WHERE row_text LIKE '%' || @a || '%'
        """;

    /// <summary>Every family of the erased account is gone, the tenant-key delegate ran once after the commit, and only the audit rows (by keyed hash) and other accounts remain.</summary>
    [Fact]
    public async Task EraseDeletesEveryFamilyOfTheOwnerAndLeavesOnlyKeyedHashAuditRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var alice = "acct-alice-" + Guid.NewGuid().ToString("N");
            var bob = "acct-bob-" + Guid.NewGuid().ToString("N");
            await SeedAsync(dataSource, alice, HashOf(alice), ct).ConfigureAwait(true);
            await SeedAsync(dataSource, bob, HashOf(bob), ct).ConfigureAwait(true);
            (await CountsAsync(dataSource, alice, ct).ConfigureAwait(true)).Values.ShouldAllBe(static count => count == 1L);
            (await TextScanAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBeGreaterThan(0L);

            var delegateCalls = new List<string>();
            Dictionary<string, long>? countsWhenTheDelegateRan = null;
            var erasure = new PostgresAccountErasure(dataSource);
            await erasure.EraseAsync(
                alice,
                HashOf(alice),
                async (tenantHash, token) =>
                {
                    delegateCalls.Add(tenantHash);
                    countsWhenTheDelegateRan = await CountsAsync(dataSource, alice, token).ConfigureAwait(false);
                },
                ct).ConfigureAwait(true);

            delegateCalls.ShouldBe([HashOf(alice)]);
            countsWhenTheDelegateRan.ShouldNotBeNull();
            countsWhenTheDelegateRan.ShouldContainKey("cowork_memory_entries");
            countsWhenTheDelegateRan.Values.ShouldAllBe(static count => count == 0L, "the delegate must run after the PostgreSQL families committed");

            var after = await CountsAsync(dataSource, alice, ct).ConfigureAwait(true);
            after.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(
            [
                "accounts", "artifact_grants", "artifact_versions", "artifacts", "cloud_agents", "cowork_events", "cowork_instructions", "cowork_memory_entries",
                "cowork_memory_settings", "cowork_projects", "dispatch_commands", "dispatch_devices", "dispatch_events", "session_events", "session_tool_approvals",
                "sessions",
            ]);
            after.Values.ShouldAllBe(static count => count == 0L);
            (await TextScanAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe(0L);

            // The audit row survives under the keyed hash alone, and every other account is untouched.
            (await AuditCountAsync(dataSource, HashOf(alice), ct).ConfigureAwait(true)).ShouldBe(1L);
            (await CountsAsync(dataSource, bob, ct).ConfigureAwait(true)).Values.ShouldAllBe(static count => count == 1L);
            (await AuditCountAsync(dataSource, HashOf(bob), ct).ConfigureAwait(true)).ShouldBe(1L);
            (await TextScanAsync(dataSource, bob, ct).ConfigureAwait(true)).ShouldBeGreaterThan(0L);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A second erasure of the same account deletes nothing more, and still runs the delegate once for that call.</summary>
    [Fact]
    public async Task EraseIsRepeatableAndRunsTheDelegateOncePerCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var alice = "acct-alice-" + Guid.NewGuid().ToString("N");
            await SeedAsync(dataSource, alice, HashOf(alice), ct).ConfigureAwait(true);
            var calls = 0;
            var erasure = new PostgresAccountErasure(dataSource);

            await erasure.EraseAsync(alice, HashOf(alice), (_, _) => { calls++; return Task.CompletedTask; }, ct).ConfigureAwait(true);
            await erasure.EraseAsync(alice, HashOf(alice), (_, _) => { calls++; return Task.CompletedTask; }, ct).ConfigureAwait(true);

            calls.ShouldBe(2);
            (await CountsAsync(dataSource, alice, ct).ConfigureAwait(true)).Values.ShouldAllBe(static count => count == 0L);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A failing tenant-key delegate surfaces its failure after the PostgreSQL families were deleted, so the caller can repeat the erasure.</summary>
    [Fact]
    public async Task FailingDelegateSurfacesAfterThePostgresFamiliesAreDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var alice = "acct-alice-" + Guid.NewGuid().ToString("N");
            await SeedAsync(dataSource, alice, HashOf(alice), ct).ConfigureAwait(true);
            var erasure = new PostgresAccountErasure(dataSource);

            await Should.ThrowAsync<InvalidOperationException>(
                () => erasure.EraseAsync(alice, HashOf(alice), static (_, _) => throw new InvalidOperationException("dragonfly is down"), ct)).ConfigureAwait(true);

            (await CountsAsync(dataSource, alice, ct).ConfigureAwait(true)).Values.ShouldAllBe(static count => count == 0L);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A blank account or tenant hash and a missing delegate are refused before any row is touched.</summary>
    [Fact]
    public async Task EraseRefusesBlankArgumentsBeforeTouchingAnyRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var erasure = new PostgresAccountErasure(schema.CreateDataSource());

            await Should.ThrowAsync<ArgumentException>(() => erasure.EraseAsync(" ", "hash", static (_, _) => Task.CompletedTask, ct)).ConfigureAwait(true);
            await Should.ThrowAsync<ArgumentException>(() => erasure.EraseAsync("acct", "", static (_, _) => Task.CompletedTask, ct)).ConfigureAwait(true);
            await Should.ThrowAsync<ArgumentNullException>(() => erasure.EraseAsync("acct", "hash", null!, ct)).ConfigureAwait(true);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A stand-in for the keyed tenant hash: a digest that does not contain the account identifier, as the real hash does not.</summary>
    private static string HashOf(string account) => "hash-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(account)));

    private static async Task SeedAsync(NpgsqlDataSource dataSource, string account, string hash, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(SeedSql);
        command.Parameters.AddWithValue("a", account);
        command.Parameters.AddWithValue("h", hash);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, long>> CountsAsync(NpgsqlDataSource dataSource, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(CountsSql);
        command.Parameters.AddWithValue("a", account);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            counts[reader.GetName(index)] = reader.GetInt64(index);
        }

        return counts;
    }

    private static async Task<long> AuditCountAsync(NpgsqlDataSource dataSource, string hash, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(AuditCountSql);
        command.Parameters.AddWithValue("h", hash);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private static async Task<long> TextScanAsync(NpgsqlDataSource dataSource, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(TextScanSql);
        command.Parameters.AddWithValue("a", account);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }
}
