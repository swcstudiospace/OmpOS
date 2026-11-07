using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using Autonogrammer.Contracts.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Revocation: a revoked device's socket receives the revoke frame and closes with 4001 within two seconds (D-30).</summary>
public sealed class RevocationTests
{
    /// <summary>Revoking a connected device sends it the revoke frame and closes its socket with 4001 within two seconds.</summary>
    [Fact]
    public async Task Revoking_a_connected_device_sends_revoke_and_closes_4001_within_two_seconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        using var owner = DispatchTestClient.As(host);
        var revoked = await DispatchTestClient.SendAsync(owner, HttpMethod.Delete, "/dispatch/devices/" + device.DeviceId, null, ct).ConfigureAwait(true);
        revoked.Status.ShouldBe(HttpStatusCode.NoContent);

        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(2));
        var frame = await HubTestSockets.ReceiveJsonAsync(socket, bound.Token).ConfigureAwait(true);
        frame.GetProperty("t").GetString().ShouldBe(DispatchFrameTypes.Revoke);
        (await HubTestSockets.ReceiveCloseAsync(socket, bound.Token).ConfigureAwait(true)).ShouldBe((WebSocketCloseStatus)DispatchCloseCodes.DeviceRevoked);
    }
}
