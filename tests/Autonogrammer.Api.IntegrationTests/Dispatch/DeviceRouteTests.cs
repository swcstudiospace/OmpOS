using System.Net;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>The device routes: owner-scoped reads and rename, the same 404 for a foreign or absent device, the revoke cascade, and the device cap.</summary>
public sealed class DeviceRouteTests
{
    /// <summary>The owner lists and reads its own device; a device with no presence key reads as offline.</summary>
    [Fact]
    public async Task The_owner_lists_and_reads_its_device_and_a_device_with_no_presence_key_is_offline()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);

        var list = await DispatchTestClient.SendAsync(client, HttpMethod.Get, "/dispatch/devices", null, ct).ConfigureAwait(true);
        list.Status.ShouldBe(HttpStatusCode.OK);
        using var listed = JsonDocument.Parse(list.Body);
        var row = listed.RootElement.GetProperty("devices")[0];
        row.GetProperty("deviceId").GetString().ShouldBe(deviceId);
        row.GetProperty("presence").GetString().ShouldBe("offline");

        var single = await DispatchTestClient.SendAsync(client, HttpMethod.Get, "/dispatch/devices/" + deviceId, null, ct).ConfigureAwait(true);
        single.Status.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>A rename is 1 to 64 characters after trimming; a valid rename shows in the next read.</summary>
    [Fact]
    public async Task Rename_refuses_an_empty_or_long_name_and_a_valid_rename_shows_in_the_next_read()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var path = "/dispatch/devices/" + deviceId;

        var empty = await DispatchTestClient.SendAsync(client, HttpMethod.Patch, path, "{\"name\":\"   \"}", ct).ConfigureAwait(true);
        empty.Status.ShouldBe(HttpStatusCode.BadRequest);
        empty.ProblemType().ShouldBe("/problems/invalid_name");

        var tooLong = await DispatchTestClient.SendAsync(client, HttpMethod.Patch, path, "{\"name\":\"" + new string('n', 65) + "\"}", ct).ConfigureAwait(true);
        tooLong.Status.ShouldBe(HttpStatusCode.BadRequest);

        var renamed = await DispatchTestClient.SendAsync(client, HttpMethod.Patch, path, "{\"name\":\"  Office Mac  \"}", ct).ConfigureAwait(true);
        renamed.Status.ShouldBe(HttpStatusCode.NoContent);

        var read = await DispatchTestClient.SendAsync(client, HttpMethod.Get, path, null, ct).ConfigureAwait(true);
        using var document = JsonDocument.Parse(read.Body);
        document.RootElement.GetProperty("name").GetString().ShouldBe("Office Mac");
    }

    /// <summary>A foreign or absent device is the same 404 on every device and command route, and a subject the deployment does not admit is 403 before any route runs.</summary>
    [Fact]
    public async Task A_foreign_or_absent_device_is_the_same_404_and_an_unlisted_subject_is_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Auth.AllowedSubjects.Add("other");
        using var owner = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(owner, ct).ConfigureAwait(true);
        using var foreign = DispatchTestClient.As(host, "other");
        using var stranger = DispatchTestClient.As(host, "stranger");

        foreach (var target in new[] { deviceId, "absent-device" })
        {
            var path = "/dispatch/devices/" + target;
            (await DispatchTestClient.SendAsync(foreign, HttpMethod.Get, path, null, ct).ConfigureAwait(true)).ProblemType().ShouldBe("/problems/device_not_found");
            (await DispatchTestClient.SendAsync(foreign, HttpMethod.Patch, path, "{\"name\":\"Stolen\"}", ct).ConfigureAwait(true)).Status.ShouldBe(HttpStatusCode.NotFound);
            (await DispatchTestClient.SendAsync(foreign, HttpMethod.Delete, path, null, ct).ConfigureAwait(true)).Status.ShouldBe(HttpStatusCode.NotFound);
            var enqueue = await DispatchTestClient.SendAsync(foreign, HttpMethod.Post, path + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
            enqueue.ProblemType().ShouldBe("/problems/device_not_found");
        }

        var refused = await DispatchTestClient.SendAsync(stranger, HttpMethod.Get, "/dispatch/devices/" + deviceId, null, ct).ConfigureAwait(true);
        refused.Status.ShouldBe(HttpStatusCode.Forbidden);
        refused.ProblemType().ShouldBe("/problems/subject_not_allowed");

        var untouched = await DispatchTestClient.SendAsync(owner, HttpMethod.Get, "/dispatch/devices/" + deviceId, null, ct).ConfigureAwait(true);
        using var document = JsonDocument.Parse(untouched.Body);
        document.RootElement.GetProperty("name").GetString().ShouldBe("Desk");
    }

    /// <summary>Revoking one device stops it counting, cancels its queued command with reason revoked, and refuses new commands for it with 404.</summary>
    [Fact]
    public async Task Revoking_a_device_cancels_its_queued_command_and_refuses_new_ones()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var queued = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        queued.Status.ShouldBe(HttpStatusCode.Accepted);
        using var accepted = JsonDocument.Parse(queued.Body);
        var commandId = accepted.RootElement.GetProperty("commandId").GetString();

        var revoke = await DispatchTestClient.SendAsync(client, HttpMethod.Delete, "/dispatch/devices/" + deviceId, null, ct).ConfigureAwait(true);
        revoke.Status.ShouldBe(HttpStatusCode.NoContent);

        var command = await DispatchTestClient.SendAsync(client, HttpMethod.Get, "/dispatch/commands/" + commandId, null, ct).ConfigureAwait(true);
        using var cancelled = JsonDocument.Parse(command.Body);
        cancelled.RootElement.GetProperty("state").GetString().ShouldBe("cancelled");
        cancelled.RootElement.GetProperty("reason").GetString().ShouldBe("revoked");

        var device = await DispatchTestClient.SendAsync(client, HttpMethod.Get, "/dispatch/devices/" + deviceId, null, ct).ConfigureAwait(true);
        using var revoked = JsonDocument.Parse(device.Body);
        revoked.RootElement.GetProperty("revokedAt").ValueKind.ShouldNotBe(JsonValueKind.Null);

        var refused = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        refused.Status.ShouldBe(HttpStatusCode.NotFound);
        refused.ProblemType().ShouldBe("/problems/device_not_found");
    }

    /// <summary>Revoke-all revokes every device the owner holds.</summary>
    [Fact]
    public async Task Revoke_all_revokes_every_device_the_owner_holds()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        _ = await DispatchTestClient.PairAsync(client, ct, "First").ConfigureAwait(true);
        _ = await DispatchTestClient.PairAsync(client, ct, "Second").ConfigureAwait(true);

        var revokeAll = await DispatchTestClient.SendAsync(client, HttpMethod.Delete, "/dispatch/devices", null, ct).ConfigureAwait(true);
        revokeAll.Status.ShouldBe(HttpStatusCode.NoContent);

        var list = await DispatchTestClient.SendAsync(client, HttpMethod.Get, "/dispatch/devices", null, ct).ConfigureAwait(true);
        using var document = JsonDocument.Parse(list.Body);
        var devices = document.RootElement.GetProperty("devices");
        devices.GetArrayLength().ShouldBe(2);
        foreach (var row in devices.EnumerateArray())
        {
            row.GetProperty("revokedAt").ValueKind.ShouldNotBe(JsonValueKind.Null);
        }
    }

    /// <summary>The account holds at most 20 devices: the 21st pairing is refused with device_limit_reached, and a revoked device stops counting.</summary>
    [Fact]
    public async Task The_21st_pairing_is_refused_and_a_revoked_device_stops_counting()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 20; index++)
        {
            var created = await store.CreateDeviceAsync(
                new CreateDispatchDevice(DispatchTestClient.Owner, "seed-" + index, "Seed", "seed-key", "jti-" + index, now.AddDays(30), [new DispatchCapabilitySnapshot(DispatchCapability.Sessions, 1, true)], "session-1"),
                ct).ConfigureAwait(true);
            created.Status.ShouldBe(DispatchDeviceCreateStatus.Created);
        }

        using var client = DispatchTestClient.As(host);
        var refused = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices", DispatchTestClient.PairingJson(), ct).ConfigureAwait(true);
        refused.Status.ShouldBe(HttpStatusCode.Conflict);
        refused.ProblemType().ShouldBe("/problems/device_limit_reached");

        (await store.RevokeDeviceAsync(DispatchTestClient.Owner, "seed-0", "test", now, ct).ConfigureAwait(true)).ShouldBeTrue();
        var paired = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices", DispatchTestClient.PairingJson(), ct).ConfigureAwait(true);
        paired.Status.ShouldBe(HttpStatusCode.Created);
    }
}
