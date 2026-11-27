using System.Globalization;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions.Postgres.Migrations;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests.Dispatch;

/// <summary>
/// Gap 74-G1 regression cases for the dispatch idempotency-key/CommandId conflation. Runs against a throwaway schema:
/// new unkeyed rows never occupy a caller-key identity, and a legacy ambiguous row is left untouched.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class DispatchIdempotencyGapTests(PostgresServerFixture server) : IAsyncLifetime
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

    /// <summary>Same key and fingerprint replay the original command; a different fingerprint conflicts.</summary>
    [Fact]
    public async Task SameIdempotencyKeyReturnsOriginal()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(TimeProvider.System, ct).ConfigureAwait(true);
        await store.CreateDeviceAsync(Device(), ct).ConfigureAwait(true);
        var now = DateTimeOffset.UtcNow;

        (await store.EnqueueAsync(Command("cmd-1", "key-1", "fp", now), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        var replay = await store.EnqueueAsync(Command("cmd-2", "key-1", "fp", now), ct).ConfigureAwait(true);
        replay.Status.ShouldBe(DispatchEnqueueStatus.Replayed);
        replay.Accepted!.CommandId.ShouldBe("cmd-1");
        replay.Accepted.Seq.ShouldBe(1);

        (await store.EnqueueAsync(Command("cmd-3", "key-1", "different", now), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.IdempotencyKeyReused);
    }

    /// <summary>An unkeyed command never occupies its own <c>CommandId</c> as a caller key in either order.</summary>
    [Fact]
    public async Task UnkeyedCommandIdDoesNotBecomeACallerKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(TimeProvider.System, ct).ConfigureAwait(true);
        await store.CreateDeviceAsync(Device(), ct).ConfigureAwait(true);
        var now = DateTimeOffset.UtcNow;

        // Unkeyed first, then a keyed request whose key equals the unkeyed CommandId.
        (await store.EnqueueAsync(Command("cmd-unkeyed", null, "fp", now), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        (await store.EnqueueAsync(Command("cmd-keyed", "cmd-unkeyed", "fp", now), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        (await store.GetCommandAsync("acct", "cmd-unkeyed", ct).ConfigureAwait(true))!.IdempotencyKey.ShouldBeNull();
        (await store.GetCommandAsync("acct", "cmd-keyed", ct).ConfigureAwait(true))!.IdempotencyKey.ShouldBe("cmd-unkeyed");
        (await store.ReplayAsync(Command("cmd-again", "cmd-unkeyed", "fp", now), ct).ConfigureAwait(true))!.Accepted!.CommandId.ShouldBe("cmd-keyed");
    }

    /// <summary>A keyed row whose key equals a later unkeyed <c>CommandId</c> collides with nothing.</summary>
    [Fact]
    public async Task KeyedThenUnkeyedWithSameValueDoesNotCollide()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(TimeProvider.System, ct).ConfigureAwait(true);
        await store.CreateDeviceAsync(Device(), ct).ConfigureAwait(true);
        var now = DateTimeOffset.UtcNow;

        (await store.EnqueueAsync(Command("cmd-1", "shared-value", "fp", now), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        (await store.EnqueueAsync(Command("shared-value", null, "fp", now), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        (await store.GetCommandAsync("acct", "shared-value", ct).ConfigureAwait(true))!.IdempotencyKey.ShouldBeNull();
        (await store.GetCommandAsync("acct", "cmd-1", ct).ConfigureAwait(true))!.IdempotencyKey.ShouldBe("shared-value");
    }

    /// <summary>A caller key equal to its own <c>CommandId</c> is a genuinely keyed row and replays itself.</summary>
    [Fact]
    public async Task KeyEqualToOwnIdReplaysItself()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(TimeProvider.System, ct).ConfigureAwait(true);
        await store.CreateDeviceAsync(Device(), ct).ConfigureAwait(true);
        var now = DateTimeOffset.UtcNow;

        (await store.EnqueueAsync(Command("cmd-own", "cmd-own", "fp", now), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        var replay = await store.EnqueueAsync(Command("cmd-retry", "cmd-own", "fp", now), ct).ConfigureAwait(true);
        replay.Status.ShouldBe(DispatchEnqueueStatus.Replayed);
        replay.Accepted!.CommandId.ShouldBe("cmd-own");
        (await store.GetCommandAsync("acct", "cmd-own", ct).ConfigureAwait(true))!.IdempotencyKey.ShouldBe("cmd-own");
    }

    /// <summary>Unkeyed rows read back <c>null</c> on every read path, before and after transition and purge; keyed rows keep their key.</summary>
    [Fact]
    public async Task NullKeyRoundTripsOnEveryReadPath()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var store = await CreateStoreAsync(clock, ct).ConfigureAwait(true);
        await store.CreateDeviceAsync(Device(), ct).ConfigureAwait(true);

        await store.EnqueueAsync(Command("cmd-unkeyed", null, "fp", clock.UtcNow), ct).ConfigureAwait(true);
        await store.EnqueueAsync(Command("cmd-keyed", "key-1", "fp", clock.UtcNow), ct).ConfigureAwait(true);

        (await store.GetCommandAsync("acct", "cmd-unkeyed", ct).ConfigureAwait(true))!.IdempotencyKey.ShouldBeNull();
        (await store.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true)).Single(static item => string.Equals(item.CommandId, "cmd-unkeyed", StringComparison.Ordinal)).IdempotencyKey.ShouldBeNull();
        (await store.ListCommandsAsync("acct", "dev", 10, ct).ConfigureAwait(true)).Single(static item => string.Equals(item.CommandId, "cmd-unkeyed", StringComparison.Ordinal)).IdempotencyKey.ShouldBeNull();

        await store.TryTransitionAsync("acct", "cmd-unkeyed", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, null, ct).ConfigureAwait(true);
        (await store.GetCommandAsync("acct", "cmd-unkeyed", ct).ConfigureAwait(true))!.IdempotencyKey.ShouldBeNull();

        await store.TryTransitionAsync("acct", "cmd-unkeyed", DispatchCommandState.Delivered, DispatchCommandState.Expired, DispatchFailureReason.Expired, null, ct).ConfigureAwait(true);
        clock.UtcNow = clock.UtcNow.AddDays(8);
        (await store.GetCommandAsync("acct", "cmd-unkeyed", ct).ConfigureAwait(true))!.IdempotencyKey.ShouldBeNull();
        (await store.ListCommandsAsync("acct", "dev", 10, ct).ConfigureAwait(true)).Single(static item => string.Equals(item.CommandId, "cmd-unkeyed", StringComparison.Ordinal)).IdempotencyKey.ShouldBeNull();
        (await store.ListCommandsAsync("acct", "dev", 10, ct).ConfigureAwait(true)).Single(static item => string.Equals(item.CommandId, "cmd-keyed", StringComparison.Ordinal)).IdempotencyKey.ShouldBe("key-1");
    }

    /// <summary>A legacy ambiguous row is untouched: reads and rewrites change no origin-bearing value and add no origin member.</summary>
    [Fact]
    public async Task LegacyAmbiguousRowIsUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(TimeProvider.System, ct).ConfigureAwait(true);
        var schema = _schemas[^1];
        await store.CreateDeviceAsync(Device(), ct).ConfigureAwait(true);
        var now = DateTimeOffset.UtcNow;
        await InsertLegacyRowAsync(schema, "legacy-1", now, ct).ConfigureAwait(true);

        // Pre-existing behavior preserved: the stored value projects as before, with no classification.
        (await store.GetCommandAsync("acct", "legacy-1", ct).ConfigureAwait(true))!.IdempotencyKey.ShouldBe("legacy-1");

        await store.TryTransitionAsync("acct", "legacy-1", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, null, ct).ConfigureAwait(true);
        (await store.GetCommandAsync("acct", "legacy-1", ct).ConfigureAwait(true))!.IdempotencyKey.ShouldBe("legacy-1");

        // No backfill on read, no origin member after a transition rewrite.
        var connection = await schema.CreateDataSource().OpenConnectionAsync(ct).ConfigureAwait(true);
        await using var scope = connection.ConfigureAwait(true);
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT idempotency_key, result->>'keyOrigin' FROM dispatch_commands WHERE id = 'legacy-1'";
        var reader = await query.ExecuteReaderAsync(ct).ConfigureAwait(true);
        await using (reader.ConfigureAwait(true))
        {
            await reader.ReadAsync(ct).ConfigureAwait(true);
            reader.GetString(0).ShouldBe("legacy-1");
            (await reader.IsDBNullAsync(1, ct).ConfigureAwait(true)).ShouldBeTrue();
        }
    }

    /// <summary>Two store instances over one schema serialize concurrent equal-key inserts: one accepted, one replay of it.</summary>
    [Fact]
    public async Task ConcurrentSameKeyEnqueuesExactlyOneOriginal()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await OpenSchemaAsync(ct).ConfigureAwait(true);
        var first = new PostgresDispatchStore(schema.CreateDataSource(), DispatchStoreLimits.Default, TimeProvider.System, static _ => "tenant-hash");
        var second = new PostgresDispatchStore(schema.CreateDataSource(), DispatchStoreLimits.Default, TimeProvider.System, static _ => "tenant-hash");
        await first.CreateDeviceAsync(Device(), ct).ConfigureAwait(true);
        var now = DateTimeOffset.UtcNow;

        var one = first.EnqueueAsync(Command("cmd-a", "key-race", "fp", now), ct).AsTask();
        var two = second.EnqueueAsync(Command("cmd-b", "key-race", "fp", now), ct).AsTask();
        var results = await Task.WhenAll(one, two).ConfigureAwait(true);

        results.Count(static item => item.Status == DispatchEnqueueStatus.Accepted).ShouldBe(1);
        results.Count(static item => item.Status == DispatchEnqueueStatus.Replayed).ShouldBe(1);
        results[0].Accepted!.CommandId.ShouldBe(results[1].Accepted!.CommandId);
        results[0].Accepted!.Seq.ShouldBe(results[1].Accepted!.Seq);
    }

    /// <summary>The same caller key on another device is independent; replay never crosses the device boundary.</summary>
    [Fact]
    public async Task SameKeyOnAnotherDeviceIsIndependent()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(TimeProvider.System, ct).ConfigureAwait(true);
        await store.CreateDeviceAsync(Device("dev-1"), ct).ConfigureAwait(true);
        await store.CreateDeviceAsync(Device("dev-2"), ct).ConfigureAwait(true);
        var now = DateTimeOffset.UtcNow;

        (await store.EnqueueAsync(Command("cmd-1", "shared-key", "fp", now, "dev-1"), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        (await store.EnqueueAsync(Command("cmd-2", "shared-key", "fp", now, "dev-2"), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        (await store.ReplayAsync(Command("cmd-x", "shared-key", "fp", now, "dev-1"), ct).ConfigureAwait(true))!.Accepted!.CommandId.ShouldBe("cmd-1");
        (await store.ReplayAsync(Command("cmd-x", "shared-key", "fp", now, "dev-2"), ct).ConfigureAwait(true))!.Accepted!.CommandId.ShouldBe("cmd-2");
    }

    private async Task<PostgresDispatchStore> CreateStoreAsync(TimeProvider time, CancellationToken cancellationToken)
    {
        var schema = await OpenSchemaAsync(cancellationToken).ConfigureAwait(false);
        var resilience = PostgresResilience.Create(new PostgresResilienceOptions
        {
            PoolInteractive = 64,
            PoolBackground = 8,
            InteractiveCommandSeconds = 30,
            BudgetSeconds = 60,
        });
        return new PostgresDispatchStore(schema.CreateDataSource(), DispatchStoreLimits.Default, time, static _ => "tenant-hash", resilience);
    }

    private async Task<PostgresTestSchema> OpenSchemaAsync(CancellationToken cancellationToken)
    {
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, cancellationToken).ConfigureAwait(false);
        _schemas.Add(schema);
        var connection = await schema.CreateDataSource().OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionScope = connection.ConfigureAwait(false);
        using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Apply the built-in M0010 UpSql unchanged to this throwaway schema; no external input.
        command.CommandText = M0010Up;
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return schema;
    }

    private static async Task InsertLegacyRowAsync(PostgresTestSchema schema, string commandId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // The pre-fix row shape: key column holds the CommandId, result document carries no origin member.
        var issued = now.ToString("o", CultureInfo.InvariantCulture);
        var result = "{\"fp\":\"fp\",\"cap\":\"sessions\",\"issued\":\"" + issued + "\",\"reason\":null,\"text\":null,\"terminalAt\":null,\"progressCount\":0}";
        var connection = await schema.CreateDataSource().OpenConnectionAsync(cancellationToken).ConfigureAwait(true);
        await using var scope = connection.ConfigureAwait(true);
        using var insert = connection.CreateCommand();
#pragma warning disable CA2100 // Insert a fixed synthetic row in the legacy shape into the throwaway schema; no external input.
        insert.CommandText = "INSERT INTO dispatch_commands (id, device_id, account_id, seq, op, state, idempotency_key, expires_at, body, result, created_at, updated_at) VALUES ('" + commandId + "', 'dev', 'acct', 1, 'session.list', 'queued', '" + commandId + "', '" + now.AddMinutes(5).ToString("o", CultureInfo.InvariantCulture) + "', CAST('{}' AS jsonb), CAST('" + result + "' AS jsonb), '" + issued + "', '" + issued + "')";
#pragma warning restore CA2100
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
    }

    private static CreateDispatchDevice Device(string deviceId = "dev") =>
        new("acct", deviceId, "device", null, null, null, [], null);

    private static EnqueueDispatchCommand Command(string commandId, string? key, string fingerprint, DateTimeOffset now, string deviceId = "dev") =>
        new("acct", deviceId, commandId, key, fingerprint, DispatchCapability.Sessions, DispatchOp.SessionList, "{}", now, now.AddHours(1));

    private static readonly string M0010Up = new M0010_DispatchDeviceState().UpSql;

    private sealed class MutableTime : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
