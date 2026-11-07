using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Delivery: a queued command reaches the connected device as a signed command frame, and the frame carries the body the owner sent (D-17, D-21).</summary>
public sealed class DeliveryTests
{
    /// <summary>A command queued while the device is connected arrives as a command frame whose signature verifies, and whose payload is the body that was sent.</summary>
    [Fact]
    public async Task A_queued_command_reaches_the_connected_device_as_a_frame_its_command_key_signs()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        using var owner = DispatchTestClient.As(host);
        var accepted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + device.DeviceId + "/commands", DispatchTestClient.CommandJson(payloadJson: "{\"n\":1}"), ct).ConfigureAwait(true);
        accepted.Status.ShouldBe(HttpStatusCode.Accepted);

        var frame = await HubTestSockets.ReceiveJsonAsync(socket, ct).ConfigureAwait(true);
        frame.GetProperty("t").GetString().ShouldBe("cmd");
        frame.GetProperty("commandId").GetString().ShouldBe(HubTestSockets.Field(accepted.Body, "commandId"));
        Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(frame.GetProperty("payload").GetString()!)).ShouldBe("{\"n\":1}");
        HubTestSockets.VerifyCommand(frame, device).ShouldBeTrue();
    }
}
