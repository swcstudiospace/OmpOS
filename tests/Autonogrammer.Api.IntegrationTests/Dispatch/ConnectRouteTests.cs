using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Autonogrammer.Api.Dispatch;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Verifies the connect seam, credential separation and the connect limiter key.</summary>
public sealed class ConnectRouteTests
{
    [Fact]
    public async Task Connect_without_a_websocket_upgrade_answers_426_upgrade_required()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = Fresh(host);
        using var paired = await DispatchApiHost.PostJsonAsync(client, "/dispatch/devices", PairingJson(), ct).ConfigureAwait(true);
        var document = await System.Text.Json.JsonDocument.ParseAsync(await paired.Content.ReadAsStreamAsync(ct).ConfigureAwait(true), cancellationToken: ct).ConfigureAwait(true);
        using var connect = new HttpRequestMessage(HttpMethod.Get, $"/dispatch/devices/{document.RootElement.GetProperty("deviceId").GetString()}/connect");
        connect.Headers.Authorization = new AuthenticationHeaderValue("Bearer", document.RootElement.GetProperty("credential").GetString());

        using var response = await client.SendAsync(connect, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.UpgradeRequired);
        (await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true)).ShouldContain("upgrade_required", Case.Sensitive);
    }

    [Fact]
    public async Task An_access_token_is_refused_and_a_credential_is_refused_off_the_connect_route()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = Fresh(host);
        using var paired = await DispatchApiHost.PostJsonAsync(client, "/dispatch/devices", PairingJson(), ct).ConfigureAwait(true);
        var credential = (await System.Text.Json.JsonDocument.ParseAsync(await paired.Content.ReadAsStreamAsync(ct).ConfigureAwait(true), cancellationToken: ct).ConfigureAwait(true)).RootElement.GetProperty("credential").GetString();

        using var access = new HttpRequestMessage(HttpMethod.Get, "/dispatch/devices/any/connect");
        access.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-device-credential");
        (await client.SendAsync(access, ct).ConfigureAwait(true)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var other = new HttpRequestMessage(HttpMethod.Post, "/dispatch/devices") { Content = new StringContent(PairingJson(), System.Text.Encoding.UTF8, "application/json") };
        other.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        client.DefaultRequestHeaders.Remove(DispatchTestAuthHandler.SubjectHeader);
        (await client.SendAsync(other, ct).ConfigureAwait(true)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void DispatchConnect_keys_differ_per_device_id_behind_one_address()
    {
        var first = new DefaultHttpContext();
        first.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.8");
        first.Request.RouteValues["id"] = "device-a";
        var second = new DefaultHttpContext();
        second.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.8");
        second.Request.RouteValues["id"] = "device-b";

        var left = DispatchRateLimits.DispatchConnectRateLimiterPolicy.PartitionKey(first);
        var right = DispatchRateLimits.DispatchConnectRateLimiterPolicy.PartitionKey(second);

        left.ShouldContain("device-a", Case.Sensitive);
        right.ShouldContain("device-b", Case.Sensitive);
        string.Equals(left, right, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public async Task Pairing_past_the_hourly_budget_answers_429()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = Fresh(host);
        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i < 6; i++)
        {
            using var response = await DispatchApiHost.PostJsonAsync(client, "/dispatch/devices", PairingJson(), ct).ConfigureAwait(true);
            last = response.StatusCode;
            if (last == HttpStatusCode.TooManyRequests)
            {
                response.Headers.RetryAfter.ShouldNotBeNull();
                return;
            }
        }

        last.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private static HttpClient Fresh(DispatchApiHost host)
    {
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.SubjectHeader, "acct");
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.SessionHeader, "session-1");
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.AmrHeader, DispatchTestAuthHandler.Amr("password", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        return client;
    }

    private static string PairingJson()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var x = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(parameters.Q.X);
        var y = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(parameters.Q.Y);
        return "{\"name\":\"Desk\",\"platform\":\"linux\",\"appVersion\":\"1\",\"protocolVersions\":[\"1\"],\"capabilities\":[{\"id\":\"sessions\",\"version\":1,\"enabled\":true}],\"publicKey\":{\"kty\":\"EC\",\"crv\":\"P-256\",\"x\":\"" + x + "\",\"y\":\"" + y + "\"}}";
    }
}
