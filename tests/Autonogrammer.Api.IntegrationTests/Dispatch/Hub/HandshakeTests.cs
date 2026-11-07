using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>The hub handshake: the plain-request refusal, the challenge, the single-use nonce, the signed hello and its welcome, and newest-wins (D-14, D-16).</summary>
public sealed class HandshakeTests
{
    /// <summary>A plain request to the connect route is refused, because the route upgrades only a WebSocket request.</summary>
    [Fact]
    public async Task A_plain_request_to_the_connect_route_is_426()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (deviceId, credential, key) = await PairWithKeyAsync(host, ct).ConfigureAwait(true);
        using var key0 = key;
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/dispatch/devices/" + deviceId + "/connect");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);

        using var response = await client.SendAsync(request, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.UpgradeRequired);
    }

    /// <summary>A hello signed by the registered key, with the nonce the hub sent, is answered with the welcome that resumes from the device's sequence.</summary>
    [Fact]
    public async Task A_valid_hello_gets_the_welcome_resuming_from_the_devices_sequence()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (deviceId, credential, key) = await PairWithKeyAsync(host, ct).ConfigureAwait(true);
        using var key0 = key;
        using var socket = await ConnectAsync(host, deviceId, credential, ct).ConfigureAwait(true);
        var challenge = await ReceiveJsonAsync(socket, ct).ConfigureAwait(true);
        challenge.GetProperty("t").GetString().ShouldBe("challenge");

        await SendHelloAsync(socket, key, deviceId, challenge.GetProperty("nonce").GetString()!, challenge.GetProperty("origin").GetString()!, 7, ct).ConfigureAwait(true);

        var welcome = await ReceiveJsonAsync(socket, ct).ConfigureAwait(true);
        welcome.GetProperty("t").GetString().ShouldBe("welcome");
        welcome.GetProperty("resumeFromSeq").GetInt64().ShouldBe(7);
        welcome.GetProperty("kids").GetArrayLength().ShouldBeGreaterThan(0);
    }

    /// <summary>A hello signed by a key other than the registered one closes the socket with 4002.</summary>
    [Fact]
    public async Task A_hello_signed_by_another_key_is_closed_4002()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (deviceId, credential, key) = await PairWithKeyAsync(host, ct).ConfigureAwait(true);
        using var key0 = key;
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var socket = await ConnectAsync(host, deviceId, credential, ct).ConfigureAwait(true);
        var challenge = await ReceiveJsonAsync(socket, ct).ConfigureAwait(true);

        await SendHelloAsync(socket, other, deviceId, challenge.GetProperty("nonce").GetString()!, challenge.GetProperty("origin").GetString()!, 0, ct).ConfigureAwait(true);

        (await ReceiveCloseAsync(socket, ct).ConfigureAwait(true)).ShouldBe((WebSocketCloseStatus)4002);
    }

    /// <summary>A nonce that a completed hello consumed cannot be presented on a second connection; that hello closes with 4002.</summary>
    [Fact]
    public async Task A_nonce_a_completed_hello_used_cannot_be_presented_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (deviceId, credential, key) = await PairWithKeyAsync(host, ct).ConfigureAwait(true);
        using var key0 = key;
        using var first = await ConnectAsync(host, deviceId, credential, ct).ConfigureAwait(true);
        var firstChallenge = await ReceiveJsonAsync(first, ct).ConfigureAwait(true);
        var usedNonce = firstChallenge.GetProperty("nonce").GetString()!;
        var origin = firstChallenge.GetProperty("origin").GetString()!;
        await SendHelloAsync(first, key, deviceId, usedNonce, origin, 0, ct).ConfigureAwait(true);
        (await ReceiveJsonAsync(first, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        using var second = await ConnectAsync(host, deviceId, credential, ct).ConfigureAwait(true);
        _ = await ReceiveJsonAsync(second, ct).ConfigureAwait(true);
        await SendHelloAsync(second, key, deviceId, usedNonce, origin, 0, ct).ConfigureAwait(true);

        (await ReceiveCloseAsync(second, ct).ConfigureAwait(true)).ShouldBe((WebSocketCloseStatus)4002);
    }

    /// <summary>A newer connection for the device closes the older one with 4009, so the newest connection wins.</summary>
    [Fact]
    public async Task A_newer_connection_for_the_device_closes_the_older_with_4009()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (deviceId, credential, key) = await PairWithKeyAsync(host, ct).ConfigureAwait(true);
        using var key0 = key;
        using var older = await ConnectAsync(host, deviceId, credential, ct).ConfigureAwait(true);
        var olderChallenge = await ReceiveJsonAsync(older, ct).ConfigureAwait(true);
        await SendHelloAsync(older, key, deviceId, olderChallenge.GetProperty("nonce").GetString()!, olderChallenge.GetProperty("origin").GetString()!, 0, ct).ConfigureAwait(true);
        (await ReceiveJsonAsync(older, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        using var newer = await ConnectAsync(host, deviceId, credential, ct).ConfigureAwait(true);
        var newerChallenge = await ReceiveJsonAsync(newer, ct).ConfigureAwait(true);
        await SendHelloAsync(newer, key, deviceId, newerChallenge.GetProperty("nonce").GetString()!, newerChallenge.GetProperty("origin").GetString()!, 0, ct).ConfigureAwait(true);
        (await ReceiveJsonAsync(newer, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        (await ReceiveCloseAsync(older, ct).ConfigureAwait(true)).ShouldBe((WebSocketCloseStatus)4009);
    }

    /// <summary>Pairs a device and keeps its private key, so the test can sign a hello as the device.</summary>
    private static async Task<(string DeviceId, string Credential, ECDsa Key)> PairWithKeyAsync(DispatchApiHost host, CancellationToken cancellationToken)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var body = "{\"name\":\"Desk\",\"platform\":\"linux\",\"appVersion\":\"1\",\"protocolVersions\":[\"1\"],\"capabilities\":[{\"id\":\"sessions\",\"version\":1,\"enabled\":true}],\"publicKey\":{\"kty\":\"EC\",\"crv\":\"P-256\",\"x\":\"" + Base64UrlEncoder.Encode(parameters.Q.X) + "\",\"y\":\"" + Base64UrlEncoder.Encode(parameters.Q.Y) + "\"}}";
        using var client = DispatchTestClient.As(host);
        var reply = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices", body, cancellationToken).ConfigureAwait(true);
        reply.Status.ShouldBe(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(reply.Body);
        return (document.RootElement.GetProperty("deviceId").GetString()!, document.RootElement.GetProperty("credential").GetString()!, key);
    }

    private static async Task<WebSocket> ConnectAsync(DispatchApiHost host, string deviceId, string credential, CancellationToken cancellationToken)
    {
        var client = host.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers["Authorization"] = "Bearer " + credential;
        return await client.ConnectAsync(new Uri("ws://localhost/dispatch/devices/" + deviceId + "/connect"), cancellationToken).ConfigureAwait(true);
    }

    private static async Task SendHelloAsync(WebSocket socket, ECDsa key, string deviceId, string nonce, string origin, long lastAppliedSeq, CancellationToken cancellationToken)
    {
        var canonical = DispatchHelloCanonical.Build(nonce, deviceId, origin, lastAppliedSeq);
        var signature = Base64UrlEncoder.Encode(key.SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var json = "{\"v\":1,\"t\":\"hello\",\"signature\":\"" + signature + "\",\"nonce\":\"" + nonce + "\",\"deviceId\":\"" + deviceId + "\",\"origin\":\"" + origin + "\",\"lastAppliedSeq\":" + lastAppliedSeq.ToString(CultureInfo.InvariantCulture) + "}";
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(true);
    }

    private static async Task<JsonElement> ReceiveJsonAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(true);
        result.MessageType.ShouldBe(WebSocketMessageType.Text);
        using var document = JsonDocument.Parse(buffer.AsMemory(0, result.Count));
        return document.RootElement.Clone();
    }

    private static async Task<WebSocketCloseStatus?> ReceiveCloseAsync(WebSocket socket, CancellationToken cancellationToken)
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
}
