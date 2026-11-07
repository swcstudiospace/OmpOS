using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Device sockets for the hub suites: a paired device, the connect, the signed hello, and the reads and checks of the frames the hub sends.</summary>
internal static class HubTestSockets
{
    /// <summary>A paired device: its identifier, its credential, the private key the test holds, and the public command key the hub signs with.</summary>
    /// <param name="DeviceId">The device identifier.</param>
    /// <param name="Credential">The device credential.</param>
    /// <param name="Key">The device's private key, owned by the test that created it.</param>
    /// <param name="CommandKeyX">The X coordinate of the command key, base64url.</param>
    /// <param name="CommandKeyY">The Y coordinate of the command key, base64url.</param>
    internal sealed record PairedDevice(string DeviceId, string Credential, ECDsa Key, string CommandKeyX, string CommandKeyY);

    /// <summary>Pairs a device with the given key.</summary>
    /// <param name="host">The host.</param>
    /// <param name="key">The device's key. The caller owns and disposes it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The paired device.</returns>
    internal static async Task<PairedDevice> PairAsync(DispatchApiHost host, ECDsa key, CancellationToken cancellationToken)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var body = "{\"name\":\"Desk\",\"platform\":\"linux\",\"appVersion\":\"1\",\"protocolVersions\":[\"1\"],\"capabilities\":[{\"id\":\"sessions\",\"version\":1,\"enabled\":true}],\"publicKey\":{\"kty\":\"EC\",\"crv\":\"P-256\",\"x\":\"" + Base64UrlEncoder.Encode(parameters.Q.X) + "\",\"y\":\"" + Base64UrlEncoder.Encode(parameters.Q.Y) + "\"}}";
        using var client = DispatchTestClient.As(host);
        var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices", body, cancellationToken).ConfigureAwait(true);
        reply.Status.ShouldBe(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(reply.Body);
        var root = document.RootElement;
        var commandKey = root.GetProperty("verificationKeys")[0].GetProperty("jwk");
        return new PairedDevice(
            root.GetProperty("deviceId").GetString()!,
            root.GetProperty("credential").GetString()!,
            key,
            commandKey.GetProperty("x").GetString()!,
            commandKey.GetProperty("y").GetString()!);
    }

    /// <summary>Connects a device socket with the device credential as the bearer token.</summary>
    /// <param name="host">The host.</param>
    /// <param name="device">The paired device.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted socket.</returns>
    internal static async Task<WebSocket> ConnectAsync(DispatchApiHost host, PairedDevice device, CancellationToken cancellationToken)
    {
        var client = host.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers["Authorization"] = "Bearer " + device.Credential;
        return await client.ConnectAsync(new Uri("ws://localhost/dispatch/devices/" + device.DeviceId + "/connect"), cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Reads the challenge, sends a hello signed by the device, and returns the welcome.</summary>
    /// <param name="socket">The socket.</param>
    /// <param name="device">The paired device.</param>
    /// <param name="lastAppliedSeq">The last sequence the device has applied.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The welcome frame.</returns>
    internal static async Task<JsonElement> HelloAsync(WebSocket socket, PairedDevice device, long lastAppliedSeq, CancellationToken cancellationToken)
    {
        var challenge = await ReceiveJsonAsync(socket, cancellationToken).ConfigureAwait(true);
        challenge.GetProperty("t").GetString().ShouldBe("challenge");
        var nonce = challenge.GetProperty("nonce").GetString()!;
        var origin = challenge.GetProperty("origin").GetString()!;
        var canonical = DispatchHelloCanonical.Build(nonce, device.DeviceId, origin, lastAppliedSeq);
        var signature = Base64UrlEncoder.Encode(device.Key.SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var json = "{\"v\":1,\"t\":\"hello\",\"signature\":\"" + signature + "\",\"nonce\":\"" + nonce + "\",\"deviceId\":\"" + device.DeviceId + "\",\"origin\":\"" + origin + "\",\"lastAppliedSeq\":" + lastAppliedSeq.ToString(CultureInfo.InvariantCulture) + "}";
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(true);
        return await ReceiveJsonAsync(socket, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Receives one text frame and parses it.</summary>
    /// <param name="socket">The socket.</param>
    /// <param name="cancellationToken">Cancels the receive.</param>
    /// <returns>The frame.</returns>
    internal static async Task<JsonElement> ReceiveJsonAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(true);
        result.MessageType.ShouldBe(WebSocketMessageType.Text);
        using var document = JsonDocument.Parse(buffer.AsMemory(0, result.Count));
        return document.RootElement.Clone();
    }

    /// <summary>Reads frames until the peer's close, and returns its close status.</summary>
    /// <param name="socket">The socket.</param>
    /// <param name="cancellationToken">Bounds the wait.</param>
    /// <returns>The close status.</returns>
    internal static async Task<WebSocketCloseStatus?> ReceiveCloseAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(true);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return result.CloseStatus;
            }
        }
    }

    /// <summary>Verifies a command frame: rebuilds its canonical bytes from the frame's fields and checks the command key's signature.</summary>
    /// <param name="frame">The command frame.</param>
    /// <param name="device">The paired device, which carries the command key.</param>
    /// <returns><see langword="true"/> when the signature verifies over the canonical bytes.</returns>
    internal static bool VerifyCommand(JsonElement frame, PairedDevice device)
    {
        var canonical = DispatchCanonical.Build(new DispatchCanonicalInput(
            "1",
            frame.GetProperty("commandId").GetString()!,
            frame.GetProperty("seq").GetInt64().ToString(CultureInfo.InvariantCulture),
            frame.GetProperty("userId").GetString()!,
            frame.GetProperty("deviceId").GetString()!,
            frame.GetProperty("capability").GetString()!,
            frame.GetProperty("op").GetString()!,
            frame.GetProperty("issuedAt").GetInt64().ToString(CultureInfo.InvariantCulture),
            frame.GetProperty("expiresAt").GetInt64().ToString(CultureInfo.InvariantCulture),
            frame.GetProperty("nonce").GetString()!,
            frame.GetProperty("payloadSha256").GetString()!,
            frame.GetProperty("kid").GetString()!));
        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Base64UrlEncoder.DecodeBytes(device.CommandKeyX), Y = Base64UrlEncoder.DecodeBytes(device.CommandKeyY) },
        };
        using var ecdsa = ECDsa.Create(parameters);
        return ecdsa.VerifyData(canonical, Base64UrlEncoder.DecodeBytes(frame.GetProperty("sig").GetString()!), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>Reads one string field from a JSON body.</summary>
    /// <param name="body">The JSON body.</param>
    /// <param name="name">The field.</param>
    /// <returns>The field's value.</returns>
    internal static string Field(string body, string name)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty(name).GetString()!;
    }
}
