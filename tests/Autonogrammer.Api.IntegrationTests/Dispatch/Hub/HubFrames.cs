using System.Globalization;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Raw device frames for the hub suites: a text frame sent as the device, and a signed hello that may carry extra members.</summary>
internal static class HubFrames
{
    /// <summary>Sends one text frame.</summary>
    /// <param name="socket">The socket.</param>
    /// <param name="json">The frame text.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the frame is sent.</returns>
    internal static async Task SendTextAsync(WebSocket socket, string json, CancellationToken cancellationToken)
    {
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Signs and sends a hello for the challenge the hub sent, with extra JSON members appended after the signed fields.</summary>
    /// <param name="socket">The socket.</param>
    /// <param name="device">The paired device that signs.</param>
    /// <param name="challenge">The challenge frame the hub sent.</param>
    /// <param name="lastAppliedSeq">The last sequence the device has applied.</param>
    /// <param name="extraMembers">JSON members, each prefixed with a comma, or an empty string.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the hello is sent.</returns>
    internal static async Task SendHelloAsync(WebSocket socket, HubTestSockets.PairedDevice device, JsonElement challenge, long lastAppliedSeq, string extraMembers, CancellationToken cancellationToken)
    {
        var nonce = challenge.GetProperty("nonce").GetString()!;
        var origin = challenge.GetProperty("origin").GetString()!;
        var canonical = DispatchHelloCanonical.Build(nonce, device.DeviceId, origin, lastAppliedSeq);
        var signature = Base64UrlEncoder.Encode(device.Key.SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var json = "{\"v\":1,\"t\":\"hello\",\"signature\":\"" + signature + "\",\"nonce\":\"" + nonce + "\",\"deviceId\":\"" + device.DeviceId + "\",\"origin\":\"" + origin + "\",\"lastAppliedSeq\":" + lastAppliedSeq.ToString(CultureInfo.InvariantCulture) + extraMembers + "}";
        await SendTextAsync(socket, json, cancellationToken).ConfigureAwait(true);
    }
}
