using System.Globalization;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions.Postgres.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests.Dispatch;

/// <summary>Runs the shared dispatch store contract, plus the Postgres allocation, owner and purge rules, against a throwaway schema.</summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresDispatchStoreTests(PostgresServerFixture server) : Autonogrammer.Contracts.Tests.Dispatch.Store.DispatchStoreContractTests<PostgresDispatchStore>, IAsyncLifetime
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

    /// <summary>M0010 is the step after cowork projects, and applying its DDL twice does not change the device columns.</summary>
    [Fact]
    public async Task M0010IsRegisteredAfterM0009AndIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var migration = MigrationRegistry.All.SingleOrDefault(static step => step.Version == 10);
        migration.ShouldNotBeNull("M0010 must be registered before durable Dispatch can be enabled.");
        migration.Name.ShouldBe("dispatch_device_state");

        var schema = await OpenSchemaAsync(ct).ConfigureAwait(true);
        var connection = await schema.CreateDataSource().OpenConnectionAsync(ct).ConfigureAwait(true);
        await using (connection.ConfigureAwait(true))
        {
            using var again = connection.CreateCommand();
#pragma warning disable CA2100 // Reapply the built-in M0010 UpSql unchanged to test idempotence; no external input.
            again.CommandText = M0010Up;
#pragma warning restore CA2100
            await again.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
            using var columns = connection.CreateCommand();
            columns.CommandText = ColumnSql;
            var found = new List<string>();
            var reader = await columns.ExecuteReaderAsync(ct).ConfigureAwait(true);
            await using (reader.ConfigureAwait(true))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    found.Add(reader.GetString(0));
                }
            }

            found.ShouldBe(["app_version", "capabilities", "credential_expires_at", "credential_jti", "last_source_prefix", "paired_session_id", "protocol", "revoke_reason"]);
        }
    }

    /// <summary>Thirty-two concurrent inserts allocate each sequence from 1 to 32 once.</summary>
    [Fact]
    public async Task ThirtyTwoWritersAllocateEachSequenceOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        (await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "dev", null, "jti", null, [], null), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchDeviceCreateStatus.Created);

        var tasks = new Task<DispatchEnqueueResult>[32];
        for (var i = 0; i < tasks.Length; i++)
        {
            var id = i.ToString(CultureInfo.InvariantCulture);
            tasks[i] = store.EnqueueAsync(
                new EnqueueDispatchCommand("acct", "dev", "c" + id, "k" + id, "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)),
                ct).AsTask();
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(true);
        var seqs = results.Select(static item => item.Accepted!.Seq).Order().ToArray();
        seqs.ShouldBe(Enumerable.Range(1, 32).Select(static value => (long)value).ToArray());
    }

    /// <summary>Every read path scoped to one account returns nothing for another account.</summary>
    [Fact]
    public async Task CrossOwnerReadMatrixIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "secret-name", null, "jti", null, [], null), ct).ConfigureAwait(true);
        await store.EnqueueAsync(new EnqueueDispatchCommand("acct", "dev", "c1", "k", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{\"body\":\"secret-body\"}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1)), ct).ConfigureAwait(true);
        await store.AppendFeedEventAsync("acct", "queued", "{}", ct).ConfigureAwait(true);

        (await store.GetDeviceAsync("other", "dev", ct).ConfigureAwait(true)).ShouldBeNull();
        (await store.ListDevicesAsync("other", ct).ConfigureAwait(true)).ShouldBeEmpty();
        (await store.RenameDeviceAsync("other", "dev", "nope", ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.RevokeDeviceAsync("other", "dev", "nope", DateTimeOffset.UnixEpoch, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.UpdateCredentialJtiAsync("other", "dev", "x", DateTimeOffset.UnixEpoch, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.UpdateCapabilitySnapshotAsync("other", "dev", [], ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.TouchLastSeenAsync("other", "dev", DateTimeOffset.UnixEpoch, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.UpdatePairedSessionAsync("other", "dev", "session", ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.GetCommandAsync("other", "c1", ct).ConfigureAwait(true)).ShouldBeNull();
        (await store.ListCommandsAsync("other", null, 10, ct).ConfigureAwait(true)).ShouldBeEmpty();
        (await store.ListCommandsAsync("other", "dev", 10, ct).ConfigureAwait(true)).ShouldBeEmpty();
        (await store.ReplayAsync(new EnqueueDispatchCommand("other", "dev", "c3", "k", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1)), ct).ConfigureAwait(true)).ShouldBeNull();
        (await store.ListQueuedAfterAsync("other", "dev", 0, ct).ConfigureAwait(true)).ShouldBeEmpty();
        (await store.TryTransitionAsync("other", "c1", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.NotFound);
        (await store.EnqueueAsync(new EnqueueDispatchCommand("other", "dev", "c2", "k2", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1)), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.NotFound);
        (await store.ReadFeedAsync("other", 0, ct).ConfigureAwait(true)).Events.ShouldBeEmpty();
        (await store.GetDeviceAsync("acct", "dev", ct).ConfigureAwait(true))!.Name.ShouldBe("secret-name");
    }

    /// <summary>A terminal body and its result text are cleared seven days later, and an expired command is not delivered.</summary>
    [Fact]
    public async Task PurgeClearsBodyAndExpiredCommandIsNotDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, clock, ct).ConfigureAwait(true);
        var schema = _schemas[^1];
        await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "dev", null, "jti", null, [], null), ct).ConfigureAwait(true);
        await store.EnqueueAsync(
            new EnqueueDispatchCommand("acct", "dev", "live", "k-live", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{\"body\":\"keep\"}", clock.UtcNow, clock.UtcNow.AddMinutes(5)),
            ct).ConfigureAwait(true);
        await store.EnqueueAsync(
            new EnqueueDispatchCommand("acct", "dev", "old", "k-old", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{\"body\":\"purge-me\"}", clock.UtcNow, clock.UtcNow.AddMinutes(-1)),
            ct).ConfigureAwait(true);

        (await store.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true)).Select(static item => item.CommandId).ShouldBe(["live"]);

        await store.TryTransitionAsync("acct", "old", DispatchCommandState.Queued, DispatchCommandState.Expired, DispatchFailureReason.Expired, ct).ConfigureAwait(true);
        var seedConnection = await schema.CreateDataSource().OpenConnectionAsync(ct).ConfigureAwait(true);
        await using (seedConnection.ConfigureAwait(true))
        {
            using var seed = seedConnection.CreateCommand();
            seed.CommandText = SeedResultTextSql;
            (await seed.ExecuteNonQueryAsync(ct).ConfigureAwait(true)).ShouldBe(1);
        }

        clock.UtcNow = clock.UtcNow.AddDays(7).AddTicks(1);
        var purged = await store.GetCommandAsync("acct", "old", ct).ConfigureAwait(true);
        purged!.BodyJson.ShouldBeNull();

        var verify = await schema.CreateDataSource().OpenConnectionAsync(ct).ConfigureAwait(true);
        await using (verify.ConfigureAwait(true))
        {
            using var command = verify.CreateCommand();
            command.CommandText = PurgeCheckSql;
            var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(true);
            await using (reader.ConfigureAwait(true))
            {
                var saw = false;
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    saw = true;
                    reader.GetBoolean(0).ShouldBeTrue();
                    reader.GetBoolean(1).ShouldBeTrue();
                    reader.GetBoolean(2).ShouldBeTrue();
                    reader.GetString(3).ShouldBe("tenant-hash");
                    reader.GetString(3).ShouldNotContain("acct");
                }

                saw.ShouldBeTrue();
            }
        }
    }

    /// <summary>Audit rows written with a command never hold the body or the device name.</summary>
    [Fact]
    public async Task AuditRowsOmitBodyAndName()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        var schema = _schemas[^1];
        await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "secret-name", null, "jti", null, [], null), ct).ConfigureAwait(true);
        await store.EnqueueAsync(new EnqueueDispatchCommand("acct", "dev", "c1", "k", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{\"prompt\":\"secret-body\"}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1)), ct).ConfigureAwait(true);

        var connection = await schema.CreateDataSource().OpenConnectionAsync(ct).ConfigureAwait(true);
        await using (connection.ConfigureAwait(true))
        {
            using var command = connection.CreateCommand();
            command.CommandText = AuditCountSql;
            var count = (long)(await command.ExecuteScalarAsync(ct).ConfigureAwait(true) ?? 0L);
            count.ShouldBeGreaterThan(0);
            using var scan = connection.CreateCommand();
            scan.CommandText = AuditScanSql;
            var reader = await scan.ExecuteReaderAsync(ct).ConfigureAwait(true);
            await using (reader.ConfigureAwait(true))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    var text = reader.GetString(0);
                    text.ShouldNotContain("secret-body");
                    text.ShouldNotContain("secret-name");
                    text.ShouldNotContain("prompt");
                }
            }
        }
    }

    /// <summary>Racing accounts cannot both create the same globally unique device identifier.</summary>
    [Fact]
    public async Task ConcurrentAccountsClaimingSameDeviceReturnTaken()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        var accounts = Enumerable.Range(0, 12).Select(i => "account-" + i.ToString(CultureInfo.InvariantCulture)).ToArray();
        await ProvisionAsync(_schemas[^1].CreateDataSource(), ct, accounts).ConfigureAwait(true);
        var tasks = Enumerable.Range(0, 12).Select(i =>
            store.CreateDeviceAsync(new CreateDispatchDevice("account-" + i.ToString(CultureInfo.InvariantCulture), "shared", "device", null, null, null, [], null), ct).AsTask());
        var results = await Task.WhenAll(tasks).ConfigureAwait(true);
        results.Count(static result => result.Status == DispatchDeviceCreateStatus.Created).ShouldBe(1);
        results.Count(static result => result.Status == DispatchDeviceCreateStatus.DeviceIdTaken).ShouldBe(11);
    }

    /// <summary>Competing writers respect both caps and replay does not allocate a sequence.</summary>
    [Fact]
    public async Task ConcurrentCapsAndReplayPreserveSequences()
    {
        var ct = TestContext.Current.CancellationToken;
        var limits = DispatchStoreLimits.Default with { DevicesPerAccount = 3, MaxQueuedPerDevice = 4 };
        var store = await CreateStoreAsync(limits, TimeProvider.System, ct).ConfigureAwait(true);
        var creates = await Task.WhenAll(Enumerable.Range(0, 12).Select(i =>
            store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev-" + i.ToString(CultureInfo.InvariantCulture), "device", null, null, null, [], null), ct).AsTask())).ConfigureAwait(true);
        creates.Count(static item => item.Status == DispatchDeviceCreateStatus.Created).ShouldBe(3);
        creates.Count(static item => item.Status == DispatchDeviceCreateStatus.DeviceLimitReached).ShouldBe(9);
        var device = (await store.ListDevicesAsync("acct", ct).ConfigureAwait(true))[0].DeviceId;
        var now = DateTimeOffset.UtcNow;
        var requests = Enumerable.Range(0, 12).Select(i =>
            new EnqueueDispatchCommand("acct", device, "cmd-" + i.ToString(CultureInfo.InvariantCulture), "key-" + i.ToString(CultureInfo.InvariantCulture), "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", now, now.AddHours(1))).ToArray();
        var results = await Task.WhenAll(requests.Select(request => store.EnqueueAsync(request, ct).AsTask())).ConfigureAwait(true);
        results.Count(static item => item.Status == DispatchEnqueueStatus.Accepted).ShouldBe(4);
        results.Count(static item => item.Status == DispatchEnqueueStatus.QueueFull).ShouldBe(8);
        results.Where(static item => item.Status == DispatchEnqueueStatus.Accepted).Select(static item => item.Accepted!.Seq).Order().ShouldBe([1L, 2L, 3L, 4L]);
        var acceptedIndex = Array.FindIndex(results, static item => item.Status == DispatchEnqueueStatus.Accepted);
        var replay = await store.EnqueueAsync(requests[acceptedIndex] with { CommandId = "replay" }, ct).ConfigureAwait(true);
        replay.Status.ShouldBe(DispatchEnqueueStatus.Replayed);
        replay.Accepted!.Seq.ShouldBe(results[acceptedIndex].Accepted!.Seq);
        (await store.EnqueueAsync(requests[acceptedIndex] with { CommandId = "reused", Fingerprint = "different" }, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.IdempotencyKeyReused);
    }

    /// <summary>An audit failure rolls back the associated mutation and sequence allocation.</summary>
    [Fact]
    public async Task AuditFailureRollsBackMutationsAndSequences()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await OpenSchemaAsync(ct).ConfigureAwait(true);
        await ProvisionAsync(schema.CreateDataSource(), ct, "acct").ConfigureAwait(true);
        var failAudit = false;
        var store = new PostgresDispatchStore(schema.CreateDataSource(), DispatchStoreLimits.Default, TimeProvider.System, _ => failAudit ? throw new InvalidOperationException("audit failure") : "tenant-hash");
        var device = new CreateDispatchDevice("acct", "dev", "original", null, null, null, [], null);
        failAudit = true;
        await Should.ThrowAsync<InvalidOperationException>(async () => await store.CreateDeviceAsync(device, ct).ConfigureAwait(true)).ConfigureAwait(true);
        (await store.GetDeviceAsync("acct", "dev", ct).ConfigureAwait(true)).ShouldBeNull();
        failAudit = false;
        (await store.CreateDeviceAsync(device, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchDeviceCreateStatus.Created);
        var now = DateTimeOffset.UtcNow;
        var command = new EnqueueDispatchCommand("acct", "dev", "cmd", "key", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", now, now.AddHours(1));
        failAudit = true;
        await Should.ThrowAsync<InvalidOperationException>(async () => await store.EnqueueAsync(command, ct).ConfigureAwait(true)).ConfigureAwait(true);
        (await store.GetCommandAsync("acct", "cmd", ct).ConfigureAwait(true)).ShouldBeNull();
        await Should.ThrowAsync<InvalidOperationException>(async () => await store.RenameDeviceAsync("acct", "dev", "changed", ct).ConfigureAwait(true)).ConfigureAwait(true);
        (await store.GetDeviceAsync("acct", "dev", ct).ConfigureAwait(true))!.Name.ShouldBe("original");
        await Should.ThrowAsync<InvalidOperationException>(async () => await store.AppendFeedEventAsync("acct", "queued", "{}", ct).ConfigureAwait(true)).ConfigureAwait(true);
        failAudit = false;
        (await store.EnqueueAsync(command, ct).ConfigureAwait(true)).Accepted!.Seq.ShouldBe(1);
        (await store.AppendFeedEventAsync("acct", "queued", "{}", ct).ConfigureAwait(true)).Id.ShouldBe(1);
        failAudit = true;
        await Should.ThrowAsync<InvalidOperationException>(async () => await store.TryTransitionAsync("acct", "cmd", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, ct).ConfigureAwait(true)).ConfigureAwait(true);
        (await store.GetCommandAsync("acct", "cmd", ct).ConfigureAwait(true))!.State.ShouldBe(DispatchCommandState.Queued);
    }

    /// <summary>A revoked device still supports idempotent replay, but cannot accept a new command.</summary>
    [Fact]
    public async Task RevocationDoesNotInvalidateReplay()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "device", null, null, null, [], null), ct).ConfigureAwait(true);
        var now = DateTimeOffset.UtcNow;
        var command = new EnqueueDispatchCommand("acct", "dev", "cmd", "key", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", now, now.AddHours(1));
        (await store.EnqueueAsync(command, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        (await store.RevokeDeviceAsync("acct", "dev", null, now, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await store.EnqueueAsync(command with { CommandId = "retry" }, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Replayed);
        (await store.EnqueueAsync(command with { CommandId = "new", IdempotencyKey = "new" }, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.NotFound);
    }

    /// <summary>Repeated reads do not repeat an already completed body purge or its audit entry.</summary>
    [Fact]
    public async Task PurgeIsAuditedOnlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, clock, ct).ConfigureAwait(true);
        var schema = _schemas[^1];
        await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "device", null, null, null, [], null), ct).ConfigureAwait(true);
        await store.EnqueueAsync(new EnqueueDispatchCommand("acct", "dev", "cmd", "key", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", clock.UtcNow, clock.UtcNow.AddMinutes(1)), ct).ConfigureAwait(true);
        await store.TryTransitionAsync("acct", "cmd", DispatchCommandState.Queued, DispatchCommandState.Expired, DispatchFailureReason.Expired, ct).ConfigureAwait(true);
        clock.UtcNow = clock.UtcNow.AddDays(8);
        (await store.ListCommandsAsync("acct", "dev", 10, ct).ConfigureAwait(true)).Single().BodyJson.ShouldBeNull();
        (await store.GetCommandAsync("acct", "cmd", ct).ConfigureAwait(true))!.BodyJson.ShouldBeNull();
        var connection = await schema.CreateDataSource().OpenConnectionAsync(ct).ConfigureAwait(true);
        await using var connectionScope = connection.ConfigureAwait(true);
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT count(*) FROM dispatch_audit WHERE op = 'command.purge'";
        ((long)(await query.ExecuteScalarAsync(ct).ConfigureAwait(true))!).ShouldBe(1L);
    }

    /// <summary>A device, a command and a feed event written by one store instance are read back exactly by a new instance over the same database after a restart.</summary>
    [Fact]
    public async Task RecordsWrittenBeforeRestartAreReadBackExactly()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await OpenProvisionedSchemaAsync(ct).ConfigureAwait(true);
        var clock = new MutableTime();
        await WriteRecordsBeforeRestartAsync(schema, clock, ct).ConfigureAwait(true);

        var after = NpgsqlDataSource.Create(schema.ConnectionString);
        await using (after.ConfigureAwait(true))
        {
            var reader = BuildStore(after, DispatchStoreLimits.Default, clock);
            await AssertDeviceReadBackAsync(reader, clock.UtcNow, ct).ConfigureAwait(true);
            await AssertCommandAndFeedReadBackAsync(reader, clock.UtcNow, ct).ConfigureAwait(true);
        }
    }

    private static async Task WriteRecordsBeforeRestartAsync(PostgresTestSchema schema, MutableTime clock, CancellationToken cancellationToken)
    {
        var issued = clock.UtcNow;
        var before = NpgsqlDataSource.Create(schema.ConnectionString);
        await using (before.ConfigureAwait(false))
        {
            var writer = BuildStore(before, DispatchStoreLimits.Default, clock);
            var created = await writer.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "laptop", "spki-key", "jti-1", issued.AddDays(30), RecordCapabilities, "session-1"), cancellationToken).ConfigureAwait(false);
            created.Status.ShouldBe(DispatchDeviceCreateStatus.Created);
            var accepted = await SubmitAsync(writer, "cmd-1", "key-1", "fp-1", issued, issued.AddMinutes(5), cancellationToken).ConfigureAwait(false);
            accepted.Status.ShouldBe(DispatchEnqueueStatus.Accepted);
            var feed = await writer.AppendFeedEventAsync("acct", "queued", "{}", cancellationToken).ConfigureAwait(false);
            feed.Id.ShouldBe(1);
        }
    }

    private static async Task AssertDeviceReadBackAsync(PostgresDispatchStore reader, DateTimeOffset issued, CancellationToken cancellationToken)
    {
        var device = await reader.GetDeviceAsync("acct", "dev", cancellationToken).ConfigureAwait(false);
        device.ShouldNotBeNull();
        device.Name.ShouldBe("laptop");
        device.PublicKey.ShouldBe("spki-key");
        device.CredentialJti.ShouldBe("jti-1");
        device.CredentialExpiresAt.ShouldBe(issued.AddDays(30));
        device.Capabilities.ShouldBe(RecordCapabilities);
        device.PairedSessionId.ShouldBe("session-1");
        device.RevokedAt.ShouldBeNull();
    }

    private static async Task AssertCommandAndFeedReadBackAsync(PostgresDispatchStore reader, DateTimeOffset issued, CancellationToken cancellationToken)
    {
        var command = await reader.GetCommandAsync("acct", "cmd-1", cancellationToken).ConfigureAwait(false);
        command.ShouldNotBeNull();
        command.Seq.ShouldBe(1);
        command.State.ShouldBe(DispatchCommandState.Queued);
        command.Capability.ShouldBe(DispatchCapability.Sessions);
        command.Op.ShouldBe(DispatchOp.SessionList);
        command.Fingerprint.ShouldBe("fp-1");
        command.IdempotencyKey.ShouldBe("key-1");
        command.BodyJson.ShouldBe(CanonicalBody);
        command.IssuedAt.ShouldBe(issued);
        command.ExpiresAt.ShouldBe(issued.AddMinutes(5));
        command.Reason.ShouldBeNull();

        var page = await reader.ReadFeedAsync("acct", 0, cancellationToken).ConfigureAwait(false);
        page.Resync.ShouldBeFalse();
        page.Events.Count.ShouldBe(1);
        page.Events[0].Id.ShouldBe(1);
        page.Events[0].AccountId.ShouldBe("acct");
        page.Events[0].Kind.ShouldBe("queued");
        page.Events[0].Payload.ShouldBe("{}");
    }

    /// <summary>After a restart a stored key replays the original command, a different fingerprint on that key is refused, and the device sequence continues without a gap.</summary>
    [Fact]
    public async Task IdempotencyAndSequenceContinueAfterRestart()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await OpenProvisionedSchemaAsync(ct).ConfigureAwait(true);
        var clock = new MutableTime();
        var issued = clock.UtcNow;

        var before = NpgsqlDataSource.Create(schema.ConnectionString);
        await using (before.ConfigureAwait(true))
        {
            var writer = BuildStore(before, DispatchStoreLimits.Default, clock);
            await RegisterDeviceAsync(writer, ct).ConfigureAwait(true);
            (await SubmitAsync(writer, "cmd-1", "key-1", "fp-1", issued, issued.AddMinutes(5), ct).ConfigureAwait(true)).Accepted!.Seq.ShouldBe(1);
            (await SubmitAsync(writer, "cmd-2", "key-2", "fp-2", issued, issued.AddMinutes(5), ct).ConfigureAwait(true)).Accepted!.Seq.ShouldBe(2);
        }

        var after = NpgsqlDataSource.Create(schema.ConnectionString);
        await using (after.ConfigureAwait(true))
        {
            var reader = BuildStore(after, DispatchStoreLimits.Default, clock);
            var replay = await SubmitAsync(reader, "cmd-retry", "key-1", "fp-1", issued, issued.AddMinutes(5), ct).ConfigureAwait(true);
            replay.Status.ShouldBe(DispatchEnqueueStatus.Replayed);
            replay.Accepted!.CommandId.ShouldBe("cmd-1");
            replay.Accepted.Seq.ShouldBe(1);
            var reused = await SubmitAsync(reader, "cmd-reused", "key-1", "fp-other", issued, issued.AddMinutes(5), ct).ConfigureAwait(true);
            reused.Status.ShouldBe(DispatchEnqueueStatus.IdempotencyKeyReused);
            (await reader.GetCommandAsync("acct", "cmd-retry", ct).ConfigureAwait(true)).ShouldBeNull();
            (await reader.GetCommandAsync("acct", "cmd-reused", ct).ConfigureAwait(true)).ShouldBeNull();
            var next = await SubmitAsync(reader, "cmd-3", "key-3", "fp-3", issued, issued.AddMinutes(5), ct).ConfigureAwait(true);
            next.Accepted!.Seq.ShouldBe(3);
        }
    }

    /// <summary>A queued command and a running command keep their states across a restart. The new instance lists the queued command only before its deadline, and it finishes the running one.</summary>
    [Fact]
    public async Task QueuedAndRunningCommandsSurviveRestart()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await OpenProvisionedSchemaAsync(ct).ConfigureAwait(true);
        var clock = new MutableTime();
        var issued = clock.UtcNow;

        var before = NpgsqlDataSource.Create(schema.ConnectionString);
        await using (before.ConfigureAwait(true))
        {
            var writer = BuildStore(before, DispatchStoreLimits.Default, clock);
            await RegisterDeviceAsync(writer, ct).ConfigureAwait(true);
            await SubmitAsync(writer, "queued", "k-queued", "fp", issued, issued.AddMinutes(5), ct).ConfigureAwait(true);
            await SubmitAsync(writer, "running", "k-running", "fp", issued, issued.AddMinutes(5), ct).ConfigureAwait(true);
            await SubmitAsync(writer, "short", "k-short", "fp", issued, issued.AddMinutes(2), ct).ConfigureAwait(true);
            await AdvanceToRunningAsync(writer, "running", ct).ConfigureAwait(true);
        }

        clock.UtcNow = issued.AddMinutes(1);
        var after = NpgsqlDataSource.Create(schema.ConnectionString);
        await using (after.ConfigureAwait(true))
        {
            var reader = BuildStore(after, DispatchStoreLimits.Default, clock);
            var queued = await reader.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true);
            queued.Select(static item => item.CommandId).ShouldBe(["queued", "short"]);
            queued.Select(static item => item.Seq).ShouldBe([1L, 3L]);
            var running = await reader.GetCommandAsync("acct", "running", ct).ConfigureAwait(true);
            running!.State.ShouldBe(DispatchCommandState.Running);
            running.Seq.ShouldBe(2);

            clock.UtcNow = issued.AddMinutes(3);
            (await reader.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true)).Select(static item => item.CommandId).ShouldBe(["queued"]);
            var finished = await reader.TryTransitionAsync("acct", "running", DispatchCommandState.Running, DispatchCommandState.Completed, null, ct).ConfigureAwait(true);
            finished.Status.ShouldBe(DispatchTransitionStatus.Applied);
            finished.State.ShouldBe(DispatchCommandState.Completed);
            var delivered = await reader.TryTransitionAsync("acct", "queued", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, ct).ConfigureAwait(true);
            delivered.Status.ShouldBe(DispatchTransitionStatus.Applied);

            clock.UtcNow = issued.AddMinutes(6);
            (await reader.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true)).ShouldBeEmpty();
            (await reader.GetCommandAsync("acct", "queued", ct).ConfigureAwait(true))!.State.ShouldBe(DispatchCommandState.Delivered);
        }
    }

    /// <summary>A revocation written before a restart still holds after it: the device keeps its revoked state and reason, its credential is gone, its open command is cancelled, and it accepts no new command.</summary>
    [Fact]
    public async Task RevocationSurvivesRestart()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await OpenProvisionedSchemaAsync(ct).ConfigureAwait(true);
        var clock = new MutableTime();
        var issued = clock.UtcNow;
        var revokedAt = issued.AddMinutes(2);

        var before = NpgsqlDataSource.Create(schema.ConnectionString);
        await using (before.ConfigureAwait(true))
        {
            var writer = BuildStore(before, DispatchStoreLimits.Default, clock);
            await RegisterDeviceAsync(writer, ct).ConfigureAwait(true);
            await SubmitAsync(writer, "queued", "k-queued", "fp", issued, issued.AddMinutes(5), ct).ConfigureAwait(true);
            (await writer.RevokeDeviceAsync("acct", "dev", "lost", revokedAt, ct).ConfigureAwait(true)).ShouldBeTrue();
        }

        var after = NpgsqlDataSource.Create(schema.ConnectionString);
        await using (after.ConfigureAwait(true))
        {
            var reader = BuildStore(after, DispatchStoreLimits.Default, clock);
            var device = await reader.GetDeviceAsync("acct", "dev", ct).ConfigureAwait(true);
            device.ShouldNotBeNull();
            device.RevokedAt.ShouldBe(revokedAt);
            device.RevokeReason.ShouldBe("lost");
            device.CredentialJti.ShouldBeNull();
            var rows = await reader.ListCommandsAsync("acct", "dev", 10, ct).ConfigureAwait(true);
            rows.Count.ShouldBe(1);
            rows[0].State.ShouldBe(DispatchCommandState.Cancelled);
            rows[0].Reason.ShouldBe(DispatchFailureReason.Revoked);
            (await SubmitAsync(reader, "late", "k-late", "fp", issued, issued.AddMinutes(5), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.NotFound);
            (await reader.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true)).ShouldBeEmpty();
        }
    }

    /// <inheritdoc />
    protected override async Task<PostgresDispatchStore> CreateStoreAsync(DispatchStoreLimits limits, TimeProvider time, CancellationToken cancellationToken)
    {
        var schema = await OpenSchemaAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(schema.CreateDataSource(), cancellationToken, "acct", "other").ConfigureAwait(false);
        return BuildStore(schema.CreateDataSource(), limits, time);
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

    private async Task<PostgresTestSchema> OpenProvisionedSchemaAsync(CancellationToken cancellationToken)
    {
        var schema = await OpenSchemaAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(schema.CreateDataSource(), cancellationToken, "acct").ConfigureAwait(false);
        return schema;
    }

    private static PostgresDispatchStore BuildStore(NpgsqlDataSource dataSource, DispatchStoreLimits limits, TimeProvider time)
    {
        var resilience = PostgresResilience.Create(new PostgresResilienceOptions
        {
            PoolInteractive = 64,
            PoolBackground = 8,
            InteractiveCommandSeconds = 30,
            BudgetSeconds = 60,
        });
        return new PostgresDispatchStore(dataSource, limits, time, static _ => "tenant-hash", resilience);
    }

    private static async Task RegisterDeviceAsync(PostgresDispatchStore store, CancellationToken cancellationToken)
    {
        var created = await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "laptop", null, "jti", null, [], null), cancellationToken).ConfigureAwait(false);
        created.Status.ShouldBe(DispatchDeviceCreateStatus.Created);
    }

    private static Task<DispatchEnqueueResult> SubmitAsync(
        PostgresDispatchStore store,
        string commandId,
        string key,
        string fingerprint,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken) =>
        store.EnqueueAsync(
            new EnqueueDispatchCommand("acct", "dev", commandId, key, fingerprint, DispatchCapability.Sessions, DispatchOp.SessionList, CanonicalBody, issuedAt, expiresAt),
            cancellationToken).AsTask();

    private static async Task AdvanceToRunningAsync(PostgresDispatchStore store, string commandId, CancellationToken cancellationToken)
    {
        (DispatchCommandState From, DispatchCommandState To)[] path =
        [
            (DispatchCommandState.Queued, DispatchCommandState.Delivered),
            (DispatchCommandState.Delivered, DispatchCommandState.Received),
            (DispatchCommandState.Received, DispatchCommandState.Accepted),
            (DispatchCommandState.Accepted, DispatchCommandState.Running),
        ];
        foreach (var (from, to) in path)
        {
            (await store.TryTransitionAsync("acct", commandId, from, to, null, cancellationToken).ConfigureAwait(false)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        }
    }

    /// <summary>Provisions accounts as the provisioning step does: one lifecycle row each, committed before the account's first write.</summary>
    private static async Task ProvisionAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken, params string[] accountIds)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            foreach (var accountId in accountIds)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO account_lifecycle (account_id) VALUES (@account_id) ON CONFLICT (account_id) DO NOTHING";
                command.Parameters.AddWithValue("account_id", accountId);
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static readonly string M0010Up = new M0010_DispatchDeviceState().UpSql;

    private const string ColumnSql = """
        SELECT column_name FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = 'dispatch_devices'
          AND column_name IN ('credential_jti', 'credential_expires_at', 'capabilities', 'app_version', 'protocol', 'revoke_reason', 'paired_session_id', 'last_source_prefix')
        ORDER BY column_name
        """;

    private const string SeedResultTextSql = "UPDATE dispatch_commands SET result = jsonb_set(result, '{text}', '\"result-text\"') WHERE id = 'old'";

    private const string PurgeCheckSql = "SELECT body IS NULL, body_purged_at IS NOT NULL, result->>'text' IS NULL, account_id_hash FROM dispatch_commands, dispatch_audit WHERE dispatch_commands.id = 'old'";

    private const string AuditCountSql = "SELECT count(*) FROM dispatch_audit WHERE op = 'command.enqueue'";

    private const string AuditScanSql = "SELECT coalesce(device_id, '') || coalesce(command_id, '') || op || outcome || coalesce(corr, '') || account_id_hash FROM dispatch_audit";

    private const string CanonicalBody = "{\"body\": \"kept\"}";

    private static readonly DispatchCapabilitySnapshot[] RecordCapabilities = [new DispatchCapabilitySnapshot(DispatchCapability.Sessions, 2, true)];

    private sealed class MutableTime : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
