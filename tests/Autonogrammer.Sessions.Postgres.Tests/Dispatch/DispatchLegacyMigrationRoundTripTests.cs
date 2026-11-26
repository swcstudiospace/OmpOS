using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Sessions.Postgres.Migrations;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests.Dispatch;

/// <summary>
/// Synth round-trip fixture for the 74 W2 migration (SPE-34925): legacy-shape rows are inserted at the M0009
/// shape (before <c>dispatch_device_state</c> adds the device-state columns), the migration runs to the head, and
/// the legacy rows read back unchanged with null device-state fields. Runs against a throwaway schema; requires
/// <c>AUTONOGRAMMER_TEST_POSTGRES_CONNECTION</c> (or the podman fallback) from the shared fixture.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class DispatchLegacyMigrationRoundTripTests(PostgresServerFixture server) : IAsyncLifetime
{
    private readonly List<PostgresTestSchema> _schemas = [];

    /// <inheritdoc />
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var schema in _schemas)
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Legacy device, command, feed and audit rows inserted before M0010 read back unchanged after the migration to the head.</summary>
    [Fact]
    public async Task LegacyRowsInsertedBeforeM0010SurviveMigrationToHead()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        _schemas.Add(schema);

        // Legacy shape: migrate only to M0009, so dispatch_devices has no device-state columns yet.
        var legacyRunner = new MigrationRunner(schema.CreateDataSource(), schema.SchemaName, [.. MigrationRegistry.All.Where(static step => step.Version <= 9)]);
        (await legacyRunner.MigrateAsync(ct).ConfigureAwait(true)).Count.ShouldBe(9);
        (await legacyRunner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(9);

        var beforeInsert = DateTimeOffset.UtcNow;
        await InsertLegacyRowsAsync(schema, beforeInsert, ct).ConfigureAwait(true);
        (await CountColumnsAsync(schema, "dispatch_devices", ct).ConfigureAwait(true)).ShouldBe(9);

        // Run the migration to the head; M0010 adds the device-state columns, later steps add the sweep columns.
        var runner = new MigrationRunner(schema.CreateDataSource(), schema.SchemaName);
        (await runner.MigrateAsync(ct).ConfigureAwait(true)).ShouldBe([10, 11, 12, 13, 14, 15]);
        (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(MigrationRegistry.Head);
        (await CountColumnsAsync(schema, "dispatch_devices", ct).ConfigureAwait(true)).ShouldBe(17);

        var store = new PostgresDispatchStore(schema.CreateDataSource(), DispatchStoreLimits.Default, TimeProvider.System, static _ => "tenant-hash");
        await AssertLegacyReadsAsync(store, beforeInsert, ct).ConfigureAwait(true);
        await AssertSequenceContinuesAsync(store, ct).ConfigureAwait(true);
    }

    private static async Task AssertLegacyReadsAsync(PostgresDispatchStore store, DateTimeOffset beforeInsert, CancellationToken cancellationToken)
    {
        // The legacy device reads back with its legacy values and null device-state fields.
        var device = await store.GetDeviceAsync("acct", "legacy-dev", cancellationToken).ConfigureAwait(true);
        device.ShouldNotBeNull();
        device.Name.ShouldBe("Legacy Device");
        device.PublicKey.ShouldBe("spki-legacy");
        device.CredentialJti.ShouldBeNull();
        device.CredentialExpiresAt.ShouldBeNull();
        device.Capabilities.ShouldBeEmpty();
        device.PairedSessionId.ShouldBeNull();
        device.RevokeReason.ShouldBeNull();
        device.LastSourcePrefix.ShouldBeNull();
        device.RevokedAt.ShouldBeNull();

        // The revoked legacy device keeps its revocation time.
        var revoked = await store.GetDeviceAsync("acct", "legacy-revoked", cancellationToken).ConfigureAwait(true);
        revoked.ShouldNotBeNull();
        revoked.RevokedAt.ShouldNotBeNull();
        (revoked.RevokedAt.Value - beforeInsert).ShouldBeGreaterThan(TimeSpan.FromSeconds(-1));
        (revoked.RevokedAt.Value - DateTimeOffset.UtcNow).ShouldBeLessThan(TimeSpan.FromSeconds(1));

        // The legacy command reads back with its legacy values: key still holds the CommandId, no key origin.
        var command = await store.GetCommandAsync("acct", "legacy-cmd", cancellationToken).ConfigureAwait(true);
        command.ShouldNotBeNull();
        command.Seq.ShouldBe(1);
        command.State.ShouldBe(DispatchCommandState.Queued);
        command.Op.ShouldBe(DispatchOp.SessionList);
        command.Capability.ShouldBe(DispatchCapability.Sessions);
        command.Fingerprint.ShouldBe("fp-legacy");
        command.IdempotencyKey.ShouldBe("legacy-cmd");
        command.BodyJson.ShouldBe("{}");
        command.Reason.ShouldBeNull();
        command.ResultText.ShouldBeNull();

        // The legacy feed row reads back, and the legacy queued command is still deliverable.
        var feed = await store.ReadFeedAsync("acct", 0, cancellationToken).ConfigureAwait(true);
        feed.Events.Count.ShouldBe(1);
        feed.Events[0].Id.ShouldBe(1);
        feed.Events[0].Kind.ShouldBe("command.queued");
        feed.Events[0].AccountId.ShouldBe("acct");

        var queued = await store.ListQueuedAfterAsync("acct", "legacy-dev", 0, cancellationToken).ConfigureAwait(true);
        queued.Select(static item => item.CommandId).ShouldBe(["legacy-cmd"]);
    }
    private static async Task AssertSequenceContinuesAsync(PostgresDispatchStore store, CancellationToken cancellationToken)
    {
        // Sequence allocation continues from the legacy command_seq instead of restarting at 1.
        var now = DateTimeOffset.UtcNow;
        var accepted = await store.EnqueueAsync(
            new EnqueueDispatchCommand("acct", "legacy-dev", "new-cmd", "new-key", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", now, now.AddHours(1)),
            cancellationToken).ConfigureAwait(true);
        accepted.Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        accepted.Accepted!.Seq.ShouldBe(2);
    }
    private static async Task InsertLegacyRowsAsync(PostgresTestSchema schema, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var issued = now.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        // Pre-fix result document shape: no keyOrigin member, key column holds the CommandId.
        var result = "{\"fp\":\"fp-legacy\",\"cap\":\"sessions\",\"issued\":\"" + issued + "\",\"reason\":null,\"text\":null,\"terminalAt\":null,\"progressCount\":0}";
        var connection = await schema.CreateDataSource().OpenConnectionAsync(cancellationToken).ConfigureAwait(true);
        await using var scope = connection.ConfigureAwait(true);

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO accounts (id, created_at, last_seen_at, status, feed_seq) VALUES ('acct', @now, @now, 'active', 1)";
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO dispatch_devices (id, account_id, name, platform, public_key, created_at, last_seen_at, revoked_at, command_seq) VALUES ('legacy-dev', 'acct', 'Legacy Device', 'linux', 'spki-legacy', @now, @now, NULL, 1)";
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO dispatch_devices (id, account_id, name, platform, public_key, created_at, last_seen_at, revoked_at, command_seq) VALUES ('legacy-revoked', 'acct', 'Legacy Revoked', 'linux', 'spki-revoked', @now, @now, @now, 0)";
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO dispatch_commands (id, device_id, account_id, seq, op, state, idempotency_key, expires_at, body, body_purged_at, result, created_at, updated_at) VALUES ('legacy-cmd', 'legacy-dev', 'acct', 1, 'session.list', 'queued', 'legacy-cmd', @expires, CAST('{}' AS jsonb), NULL, CAST(@result AS jsonb), @now, @now)";
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("expires", now.AddHours(1));
            command.Parameters.AddWithValue("result", result);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO dispatch_events (account_id, id, occurred_at, kind, ref, payload) VALUES ('acct', 1, @now, 'command.queued', 'legacy-cmd', CAST('{}' AS jsonb))";
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO dispatch_audit (account_id_hash, occurred_at, device_id, command_id, op, outcome, corr) VALUES ('tenant-hash', @now, 'legacy-dev', 'legacy-cmd', 'command.enqueue', 'accepted', 'legacy-corr')";
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private static async Task<long> CountColumnsAsync(PostgresTestSchema schema, string table, CancellationToken cancellationToken)
    {
        var connection = await schema.CreateDataSource().OpenConnectionAsync(cancellationToken).ConfigureAwait(true);
        await using var scope = connection.ConfigureAwait(true);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = @table";
        command.Parameters.AddWithValue("table", table);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(true) ?? 0L);
    }
}
