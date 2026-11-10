using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Autonogrammer.Api.Dispatch;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Shutdown: when the host begins to stop, the hub refuses new hellos and closes every attached socket with 1001 inside its drain budget (D-45).</summary>
public sealed class ShutdownTests
{
    /// <summary>A socket that is attached when the host begins to stop receives close 1001 within ten seconds.</summary>
    [Fact]
    public async Task Stopping_the_host_closes_an_attached_socket_with_1001_inside_ten_seconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        var stopwatch = Stopwatch.StartNew();
        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        (await HubTestSockets.ReceiveCloseAsync(socket, bound.Token).ConfigureAwait(true)).ShouldBe((WebSocketCloseStatus)DispatchCloseCodes.GoingAway);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
    }

    /// <summary>A connection that arrives after the host begins to stop is refused with 503 before the upgrade completes.</summary>
    [Fact]
    public async Task A_connection_arriving_after_the_stop_begins_is_refused_503()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        host.Services.GetRequiredService<DispatchHub>().BeginDrain();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/dispatch/devices/" + device.DeviceId + "/connect");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", device.Credential);
        request.Headers.TryAddWithoutValidation("Connection", "Upgrade");
        request.Headers.TryAddWithoutValidation("Upgrade", "websocket");
        request.Headers.TryAddWithoutValidation("Sec-WebSocket-Version", "13");
        request.Headers.TryAddWithoutValidation("Sec-WebSocket-Key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));
        using var response = await client.SendAsync(request, ct).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>With a one-second default request timeout, an upgraded socket outlives that timeout: the connect route is exempt (D-44, D-50).</summary>
    [Fact]
    public async Task The_default_request_timeout_does_not_end_an_upgraded_socket()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct, configure: settings => settings["Hosting:RequestTimeoutSeconds"] = "1").ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        await Task.Delay(TimeSpan.FromMilliseconds(2500), ct).ConfigureAwait(true);

        socket.State.ShouldBe(WebSocketState.Open);
        using var owner = DispatchTestClient.As(host);
        var listed = await DispatchTestClient.SendAsync(owner, HttpMethod.Get, "/dispatch/devices", null, ct).ConfigureAwait(true);
        JsonDocument.Parse(listed.Body).RootElement.GetProperty("devices")[0].GetProperty("presence").GetString().ShouldBe("online");
    }
}
