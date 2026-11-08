using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using Autonogrammer.Api.Audit;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>The data-plane-down matrix: an unreachable Dragonfly keeps attached sockets up, answers presence unknown, closes new hellos 1013, and refuses commands 503; secrets never reach a log or an audit event (D-14, D-34, D-36).</summary>
public sealed class DataPlaneDownTests
{
    /// <summary>With the nonce store unreachable, a new connection is closed 1013 instead of being challenged.</summary>
    [Fact]
    public async Task With_the_nonce_store_unreachable_a_new_connection_is_closed_1013()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct, registerServices: services => services.AddSingleton<IDispatchAuthNonces>(new UnreachableNonces())).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);

        (await HubTestSockets.ReceiveCloseAsync(socket, ct).ConfigureAwait(true)).ShouldBe((WebSocketCloseStatus)DispatchCloseCodes.TryAgainLater);
    }

    /// <summary>When presence becomes unreachable, the attached socket stays open, the device reads unknown, and a command is refused 503 with a retry hint.</summary>
    [Fact]
    public async Task With_presence_unreachable_an_attached_socket_stays_open_and_a_command_is_refused_503()
    {
        var ct = TestContext.Current.CancellationToken;
        var faults = new FaultSwitch();
        var host = await DispatchApiHost.StartAsync(ct, registerServices: services => services.AddSingleton<IDispatchPresence>(_ => new SwitchablePresence(new InMemoryDispatchPresence(TimeProvider.System), faults))).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        faults.Down = true;
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"hb\",\"lastAppliedSeq\":0,\"runningCount\":0,\"status\":\"idle\"}", ct).ConfigureAwait(true);
        await Task.Delay(TimeSpan.FromMilliseconds(400), ct).ConfigureAwait(true);
        socket.State.ShouldBe(WebSocketState.Open);

        using var owner = DispatchTestClient.As(host);
        var listed = await DispatchTestClient.SendAsync(owner, HttpMethod.Get, "/dispatch/devices", null, ct).ConfigureAwait(true);
        System.Text.Json.JsonDocument.Parse(listed.Body).RootElement.GetProperty("devices")[0].GetProperty("presence").GetString().ShouldBe("unknown");

        var refused = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        refused.Status.ShouldBe(HttpStatusCode.ServiceUnavailable);
        refused.Body.ShouldContain("data_plane_unavailable");
        refused.RetryAfter.ShouldBe("5");

        faults.Down = false;
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(5));
        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"hb\",\"lastAppliedSeq\":0,\"runningCount\":0,\"status\":\"invalid\"}", bound.Token).ConfigureAwait(true);
        (await HubTestSockets.ReceiveCloseAsync(socket, bound.Token).ConfigureAwait(true)).ShouldBe((WebSocketCloseStatus)DispatchCloseCodes.InvalidPayload);
    }

    /// <summary>A command body never reaches a log line or an audit event, and the audit event carries only its closed fields.</summary>
    [Fact]
    public async Task A_command_body_never_reaches_a_log_line_or_an_audit_event()
    {
        var ct = TestContext.Current.CancellationToken;
        const string canary = "CANARY-7405-BODY";
        var ledger = new RecordingAuditLedger();
        using var logs = new CapturingLoggerProvider();
        var host = await DispatchApiHost.StartAsync(ct, registerServices: services =>
        {
            services.AddSingleton<IAuditLedger>(ledger);
            services.AddSingleton<ILoggerProvider>(logs);
        }).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        using var owner = DispatchTestClient.As(host);
        var accepted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(payloadJson: "{\"note\":\"" + canary + "\"}"), ct).ConfigureAwait(true);
        accepted.Status.ShouldBe(HttpStatusCode.Accepted);
        var commandId = HubTestSockets.Field(accepted.Body, "commandId");
        (await HubTestSockets.ReceiveJsonAsync(socket, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("cmd");

        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"ack\",\"commandId\":\"" + commandId + "\",\"state\":\"completed\"}", ct).ConfigureAwait(true);
        for (var attempt = 0; attempt < 50 && ledger.Events.Count == 0; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(true);
        }

        ledger.Events.ShouldNotBeEmpty();
        var audit = ledger.Events;
        audit[0].Item.ShouldBe(commandId);
        audit[0].Outcome.ShouldBe("rejected");
        audit.Any(entry => entry.Action.Contains(canary, StringComparison.Ordinal) || entry.Summary.Contains(canary, StringComparison.Ordinal) || entry.Item.Contains(canary, StringComparison.Ordinal)).ShouldBeFalse();
        logs.Lines.Any(line => line.Contains(canary, StringComparison.Ordinal)).ShouldBeFalse();
    }
}
