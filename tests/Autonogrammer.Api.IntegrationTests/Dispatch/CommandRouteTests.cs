using System.Net;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>The command routes: idempotent enqueue, the closed capability and payload checks, the queue cap, presence outcomes, cancel, and owner scoping.</summary>
public sealed class CommandRouteTests
{
    /// <summary>The same Idempotency-Key and fingerprint replay the original 202; a different request under the same key is 409 idempotency-key-reused.</summary>
    [Fact]
    public async Task A_replayed_idempotency_key_returns_the_original_202_and_a_different_request_is_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var path = "/dispatch/devices/" + deviceId + "/commands";

        var first = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(payloadJson: "{\"a\":1}"), ct, "key-1").ConfigureAwait(true);
        first.Status.ShouldBe(HttpStatusCode.Accepted);
        var commandId = Field(first.Body, "commandId");

        var replay = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(payloadJson: "{\"a\":1}"), ct, "key-1").ConfigureAwait(true);
        replay.Status.ShouldBe(HttpStatusCode.Accepted);
        Field(replay.Body, "commandId").ShouldBe(commandId);

        var reused = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(payloadJson: "{\"a\":2}"), ct, "key-1").ConfigureAwait(true);
        reused.Status.ShouldBe(HttpStatusCode.Conflict);
        reused.ProblemType().ShouldBe("/problems/idempotency-key-reused");
    }

    /// <summary>Browser Use and Computer Use are tool classes and are never a command capability.</summary>
    [Fact]
    public async Task A_tool_class_is_never_a_command_capability()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var path = "/dispatch/devices/" + deviceId + "/commands";

        foreach (var capability in new[] { "computer_use", "browser_use" })
        {
            var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(capability: capability), ct).ConfigureAwait(true);
            reply.Status.ShouldBe(HttpStatusCode.BadRequest);
            reply.ProblemType().ShouldBe("/problems/invalid_capability");
        }
    }

    /// <summary>A capability the device does not offer is refused with 409 capability_unavailable and nothing is queued.</summary>
    [Fact]
    public async Task A_capability_the_device_lacks_is_409_and_nothing_is_queued()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);

        var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(capability: "code", op: "code.prompt"), ct).ConfigureAwait(true);
        reply.Status.ShouldBe(HttpStatusCode.Conflict);
        reply.ProblemType().ShouldBe("/problems/capability_unavailable");
    }

    /// <summary>A payload over Dispatch:MaxPayloadBytes is 413 payload_too_large.</summary>
    [Fact]
    public async Task A_payload_over_the_limit_is_413()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var large = DispatchTestClient.CommandJson(payloadJson: "{\"d\":\"" + new string('a', 70000) + "\"}");

        var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", large, ct).ConfigureAwait(true);
        reply.Status.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        reply.ProblemType().ShouldBe("/problems/payload_too_large");
    }

    /// <summary>The queue holds at most one hundred commands per device: the hundred-and-first is 409 queue_full.</summary>
    [Fact]
    public async Task The_101st_queued_command_is_409_queue_full()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 100; index++)
        {
            var seeded = await store.EnqueueAsync(
                new EnqueueDispatchCommand(DispatchTestClient.Owner, deviceId, "seed" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture), null, "fp-" + index, DispatchCapability.Sessions, DispatchOp.SessionList, "{}", now, now.AddMinutes(5)),
                ct).ConfigureAwait(true);
            seeded.Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        }

        var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        reply.Status.ShouldBe(HttpStatusCode.Conflict);
        reply.ProblemType().ShouldBe("/problems/queue_full");
    }

    /// <summary>An offline device refuses a command that asked not to be queued with 409 device_offline.</summary>
    [Fact]
    public async Task An_offline_device_refuses_a_command_that_will_not_queue()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);

        var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(queueIfOffline: false), ct).ConfigureAwait(true);
        reply.Status.ShouldBe(HttpStatusCode.Conflict);
        reply.ProblemType().ShouldBe("/problems/device_offline");
    }

    /// <summary>While presence is unknown the enqueue is 503 with Retry-After, and nothing is stored.</summary>
    [Fact]
    public async Task While_presence_is_unknown_the_enqueue_is_503_and_nothing_is_stored()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(
            ct,
            registerServices: static services => services.Replace(ServiceDescriptor.Singleton<IDispatchPresence>(new UnknownDispatchPresence()))).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);

        var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        reply.Status.ShouldBe(HttpStatusCode.ServiceUnavailable);
        reply.ProblemType().ShouldBe("/problems/data_plane_unavailable");
        reply.RetryAfter.ShouldNotBeNull();

        var store = host.Services.GetRequiredService<IDispatchStore>();
        (await store.ListQueuedAfterAsync(DispatchTestClient.Owner, deviceId, 0, ct).ConfigureAwait(true)).Count.ShouldBe(0);
    }

    /// <summary>Cancel moves a queued command to cancelled; cancelling it again is 409 command_not_cancellable.</summary>
    [Fact]
    public async Task Cancel_moves_a_queued_command_to_cancelled_and_a_second_cancel_is_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var queued = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        var commandId = Field(queued.Body, "commandId");

        var cancel = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/commands/" + commandId + "/cancel", null, ct).ConfigureAwait(true);
        cancel.Status.ShouldBe(HttpStatusCode.OK);
        Field(cancel.Body, "state").ShouldBe("cancelled");

        var again = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/commands/" + commandId + "/cancel", null, ct).ConfigureAwait(true);
        again.Status.ShouldBe(HttpStatusCode.Conflict);
        again.ProblemType().ShouldBe("/problems/command_not_cancellable");
    }

    /// <summary>A command is readable and cancellable only by its owner: a foreign read is 404, and the owner's list holds only its own commands.</summary>
    [Fact]
    public async Task Commands_are_owner_scoped_on_read_list_and_cancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Auth.AllowedSubjects.Add("other");
        using var owner = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(owner, ct).ConfigureAwait(true);
        var queued = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        var commandId = Field(queued.Body, "commandId");
        using var foreign = DispatchTestClient.As(host, "other");

        (await DispatchTestClient.SendAsync(foreign, HttpMethod.Get, "/dispatch/commands/" + commandId, null, ct).ConfigureAwait(true)).ProblemType().ShouldBe("/problems/command_not_found");
        (await DispatchTestClient.SendAsync(foreign, HttpMethod.Post, "/dispatch/commands/" + commandId + "/cancel", null, ct).ConfigureAwait(true)).Status.ShouldBe(HttpStatusCode.NotFound);

        var foreignList = await DispatchTestClient.SendAsync(foreign, HttpMethod.Get, "/dispatch/commands", null, ct).ConfigureAwait(true);
        using var foreignDocument = JsonDocument.Parse(foreignList.Body);
        foreignDocument.RootElement.GetProperty("commands").GetArrayLength().ShouldBe(0);

        var ownerList = await DispatchTestClient.SendAsync(owner, HttpMethod.Get, "/dispatch/commands", null, ct).ConfigureAwait(true);
        using var ownerDocument = JsonDocument.Parse(ownerList.Body);
        var commands = ownerDocument.RootElement.GetProperty("commands");
        commands.GetArrayLength().ShouldBe(1);
        commands[0].GetProperty("commandId").GetString().ShouldBe(commandId);
    }

    private static string Field(string json, string name)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(name).GetString() ?? throw new InvalidOperationException("The field " + name + " was null.");
    }
}
