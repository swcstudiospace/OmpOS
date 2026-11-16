using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Presence: a device with a live socket reads online, a closed socket reads offline again, and presence is held for 45 seconds on connect and on each heartbeat (D-32).</summary>
public sealed class PresenceTests
{
    /// <summary>A connected device reads online, and reads offline again once its socket closes.</summary>
    [Fact]
    public async Task A_connected_device_reads_online_and_a_closed_socket_reads_offline_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var owner = DispatchTestClient.As(host);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        (await PresenceOfAsync(owner, ct).ConfigureAwait(true)).ShouldBe("online");

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct).ConfigureAwait(true);
        var offline = false;
        for (var attempt = 0; attempt < 50 && !offline; attempt++)
        {
            offline = string.Equals(await PresenceOfAsync(owner, ct).ConfigureAwait(true), "offline", StringComparison.Ordinal);
            if (!offline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(true);
            }
        }

        offline.ShouldBeTrue();
    }

    /// <summary>Presence is written with a 45-second lifetime when the device connects, and again on each heartbeat.</summary>
    [Fact]
    public async Task Presence_is_held_for_45_seconds_on_connect_and_on_each_heartbeat()
    {
        var ct = TestContext.Current.CancellationToken;
        var presence = new RecordingPresence(new InMemoryDispatchPresence(TimeProvider.System));
        var host = await DispatchApiHost.StartAsync(ct, registerServices: services => services.AddSingleton<IDispatchPresence>(presence)).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
        presence.SetLifetimes.ShouldBe([TimeSpan.FromSeconds(45)]);

        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"hb\",\"lastAppliedSeq\":0,\"runningCount\":0,\"status\":\"idle\"}", ct).ConfigureAwait(true);
        await WaitUntilAsync(() => presence.RefreshLifetimes.Count > 0, ct).ConfigureAwait(true);
        presence.RefreshLifetimes.ShouldBe([TimeSpan.FromSeconds(45)]);
    }

    /// <summary>A heartbeat that reports the paused status is accepted, and a heartbeat with an unknown status closes the socket 1007.</summary>
    [Fact]
    public async Task A_paused_heartbeat_is_accepted_and_an_unknown_status_closes_1007()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"hb\",\"lastAppliedSeq\":0,\"runningCount\":0,\"status\":\"paused\"}", ct).ConfigureAwait(true);
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct).ConfigureAwait(true);
        socket.State.ShouldBe(WebSocketState.Open);

        await HubFrames.SendTextAsync(socket, "{\"v\":1,\"t\":\"hb\",\"lastAppliedSeq\":0,\"runningCount\":0,\"status\":\"sleeping\"}", ct).ConfigureAwait(true);
        (await HubTestSockets.ReceiveCloseAsync(socket, ct).ConfigureAwait(true)).ShouldBe((WebSocketCloseStatus)DispatchCloseCodes.InvalidPayload);
    }

    private static async Task<string?> PresenceOfAsync(HttpClient owner, CancellationToken cancellationToken)
    {
        var listed = await DispatchTestClient.SendAsync(owner, HttpMethod.Get, "/dispatch/devices", null, cancellationToken).ConfigureAwait(true);
        using var document = JsonDocument.Parse(listed.Body);
        return document.RootElement.GetProperty("devices")[0].GetProperty("presence").GetString();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50 && !condition(); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(true);
        }
    }
}
