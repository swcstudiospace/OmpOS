using System.Net;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>
/// The command guards: a replayed key is answered before the device and capability checks, tool classes are named only on cowork.task, a
/// computer approval is refused at the door, a command id is a ULID, and the command bucket refills one token every two seconds.
/// </summary>
public sealed class CommandGuardTests
{
    private const string Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>A replayed key returns the original 202 even after the capability is turned off; a new key under the same condition is 409.</summary>
    [Fact]
    public async Task A_replayed_key_is_answered_from_the_store_even_after_the_capability_is_turned_off()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var path = "/dispatch/devices/" + deviceId + "/commands";
        var first = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(), ct, "key-off").ConfigureAwait(true);
        first.Status.ShouldBe(HttpStatusCode.Accepted);

        var store = host.Services.GetRequiredService<IDispatchStore>();
        _ = await store.UpdateCapabilitySnapshotAsync("acct", deviceId, [new DispatchCapabilitySnapshot(DispatchCapability.Sessions, 2, false)], ct).ConfigureAwait(true);

        var replay = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(), ct, "key-off").ConfigureAwait(true);
        replay.Status.ShouldBe(HttpStatusCode.Accepted);
        Field(replay.Body, "commandId").ShouldBe(Field(first.Body, "commandId"));

        var fresh = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(), ct, "key-fresh").ConfigureAwait(true);
        fresh.Status.ShouldBe(HttpStatusCode.Conflict);
        fresh.ProblemType().ShouldBe("/problems/capability_unavailable");
    }

    /// <summary>A tool-class list is refused on every operation except cowork.task, so a session command never carries one.</summary>
    [Fact]
    public async Task A_tool_class_list_is_refused_on_an_operation_other_than_cowork_task()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);

        var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(payloadJson: "{\"toolClasses\":[\"browser_use\"]}"), ct).ConfigureAwait(true);

        reply.Status.ShouldBe(HttpStatusCode.BadRequest);
        reply.ProblemType().ShouldBe("/problems/invalid_payload");
    }

    /// <summary>A cowork.task may name the tool classes it uses; the payload check does not refuse it.</summary>
    [Fact]
    public async Task A_cowork_task_may_name_its_tool_classes()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);

        var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(capability: "cowork", op: "cowork.task", payloadJson: "{\"toolClasses\":[\"browser_use\"]}"), ct).ConfigureAwait(true);

        reply.Status.ShouldNotBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A computer decision is refused by Dispatch itself, before the device is checked.</summary>
    [Fact]
    public async Task A_computer_approval_decision_is_refused_before_the_device_is_checked()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);

        var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(capability: "cowork", op: "approval.decide", payloadJson: "{\"kind\":\"computer\",\"approvalId\":\"approval-1\",\"decision\":\"approve\"}"), ct).ConfigureAwait(true);

        reply.Status.ShouldBe(HttpStatusCode.BadRequest);
        reply.ProblemType().ShouldBe("/problems/computer_approval_refused");
    }

    /// <summary>A command id is a 26-character ULID, the identifier D-17 names.</summary>
    [Fact]
    public async Task A_command_id_is_a_26_character_ulid()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);

        var accepted = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);

        var commandId = Field(accepted.Body, "commandId");
        commandId.Length.ShouldBe(26);
        commandId.All(character => Crockford.Contains(character, StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>After the bucket is exhausted, one more command is admitted once two seconds have passed, because it refills one token every two seconds.</summary>
    [Fact]
    public async Task The_command_bucket_admits_one_more_command_after_two_seconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var path = "/dispatch/devices/" + deviceId + "/commands";
        for (var index = 0; index < 10; index++)
        {
            _ = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(payloadJson: "{\"n\":" + index + "}"), ct).ConfigureAwait(true);
        }

        var limited = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(payloadJson: "{\"n\":10}"), ct).ConfigureAwait(true);
        limited.Status.ShouldBe(HttpStatusCode.TooManyRequests);

        await Task.Delay(TimeSpan.FromSeconds(2.5), ct).ConfigureAwait(true);

        var refilled = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(payloadJson: "{\"n\":11}"), ct).ConfigureAwait(true);
        refilled.Status.ShouldBe(HttpStatusCode.Accepted);
    }

    private static string Field(string body, string name)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty(name).GetString()!;
    }
}
