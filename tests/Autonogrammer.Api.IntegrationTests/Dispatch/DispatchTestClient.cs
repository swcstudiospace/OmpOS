using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Shared client, request, pairing and command helpers for the Dispatch route tests.</summary>
internal static class DispatchTestClient
{
    /// <summary>The subject the host admits by default.</summary>
    public const string Owner = "acct";

    /// <summary>Gets a client that signs in as <paramref name="subject"/> with a fresh full sign-in.</summary>
    /// <param name="host">The host.</param>
    /// <param name="subject">The subject the client signs in as.</param>
    /// <returns>The client.</returns>
    public static HttpClient As(DispatchApiHost host, string subject = Owner)
    {
        ArgumentNullException.ThrowIfNull(host);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.SubjectHeader, subject);
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.SessionHeader, "session-1");
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.AmrHeader, DispatchTestAuthHandler.Amr("password", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        return client;
    }

    /// <summary>Sends one request and reads the reply in full.</summary>
    /// <param name="client">The client.</param>
    /// <param name="method">The method.</param>
    /// <param name="path">The relative path.</param>
    /// <param name="json">The JSON body, or null for none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="idempotencyKey">The Idempotency-Key header, or null for none.</param>
    /// <returns>The reply.</returns>
    public static async Task<TestReply> SendAsync(HttpClient client, HttpMethod method, string path, string? json, CancellationToken cancellationToken, string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var request = new HttpRequestMessage(method, path);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(true);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);
        var retryAfter = response.Headers.TryGetValues("Retry-After", out var values) ? values.FirstOrDefault() : null;
        return new TestReply(response.StatusCode, body, retryAfter);
    }

    /// <summary>Pairs a desktop that offers the sessions capability and returns its device identifier.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The device identifier.</returns>
    public static async Task<string> PairAsync(HttpClient client, CancellationToken cancellationToken, string name = "Desk")
    {
        var reply = await SendAsync(client, HttpMethod.Post, "/dispatch/devices", PairingJson(name), cancellationToken).ConfigureAwait(true);
        if (reply.Status != HttpStatusCode.Created)
        {
            throw new InvalidOperationException("Pairing failed with " + reply.Status + ": " + reply.Body);
        }

        using var document = JsonDocument.Parse(reply.Body);
        return document.RootElement.GetProperty("deviceId").GetString() ?? throw new InvalidOperationException("Pairing returned no device identifier.");
    }

    /// <summary>Builds a pairing body with one sessions capability and a fresh P-256 public key.</summary>
    /// <param name="name">The display name.</param>
    /// <returns>The JSON body.</returns>
    public static string PairingJson(string name = "Desk")
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var x = Base64UrlEncoder.Encode(parameters.Q.X);
        var y = Base64UrlEncoder.Encode(parameters.Q.Y);
        return "{\"name\":\"" + name + "\",\"platform\":\"linux\",\"appVersion\":\"1\",\"protocolVersions\":[\"1\"],\"capabilities\":[{\"id\":\"sessions\",\"version\":1,\"enabled\":true}],\"publicKey\":{\"kty\":\"EC\",\"crv\":\"P-256\",\"x\":\"" + x + "\",\"y\":\"" + y + "\"}}";
    }

    /// <summary>Builds a command body whose payload is the base64url encoding of <paramref name="payloadJson"/>.</summary>
    /// <param name="capability">The capability wire name.</param>
    /// <param name="op">The operation wire name.</param>
    /// <param name="payloadJson">The JSON the payload encodes.</param>
    /// <param name="queueIfOffline">The queueIfOffline flag, or null to omit it.</param>
    /// <returns>The JSON body.</returns>
    public static string CommandJson(string capability = "sessions", string op = "session.list", string payloadJson = "{\"v\":1}", bool? queueIfOffline = null)
    {
        var payload = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(payloadJson));
        var queue = queueIfOffline is { } value ? ",\"queueIfOffline\":" + (value ? "true" : "false") : string.Empty;
        return "{\"capability\":\"" + capability + "\",\"op\":\"" + op + "\",\"payload\":\"" + payload + "\"" + queue + "}";
    }
}
