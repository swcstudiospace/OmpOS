using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Autonogrammer.Api.Dispatch.Devices;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Verifies pairing freshness, the credential shape and route separation.</summary>
public sealed class PairingTests
{
    [Fact]
    public async Task A_fresh_sign_in_pairs_and_echoes_the_capability_snapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = Client(host, fresh: true);

        using var response = await DispatchApiHost.PostJsonAsync(client, "/dispatch/devices", BodyJson(), ct).ConfigureAwait(true);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, text);
        var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(true), cancellationToken: ct).ConfigureAwait(true);
        using (document)
        {
            var root = document.RootElement;
            root.GetProperty("deviceId").GetString().ShouldNotBeNullOrWhiteSpace();
            root.GetProperty("credential").GetString().ShouldNotBeNullOrWhiteSpace();
            root.GetProperty("hubOrigin").GetString().ShouldNotBeNullOrWhiteSpace();
            root.GetProperty("verificationKeys")[0].GetProperty("kid").GetString().ShouldBe("dispatch-command");
            root.GetProperty("verificationKeys")[0].GetProperty("jwk").GetProperty("crv").GetString().ShouldBe("P-256");
            root.GetProperty("limits").GetProperty("devicesPerAccount").GetInt32().ShouldBe(20);
            root.GetProperty("capabilities")[0].GetProperty("id").GetString().ShouldBe("browser_use");
        }
    }

    [Fact]
    public async Task A_stale_sign_in_is_403_recent_sign_in_required()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = Client(host, fresh: false);

        using var response = await DispatchApiHost.PostJsonAsync(client, "/dispatch/devices", BodyJson(), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true)).ShouldContain("recent_sign_in_required", Case.Sensitive);
    }

    [Fact]
    public async Task Renewal_invalidates_the_older_jti()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = Client(host, fresh: true);
        using var paired = await DispatchApiHost.PostJsonAsync(client, "/dispatch/devices", BodyJson(), ct).ConfigureAwait(true);
        paired.StatusCode.ShouldBe(HttpStatusCode.Created);
        var document = await JsonDocument.ParseAsync(await paired.Content.ReadAsStreamAsync(ct).ConfigureAwait(true), cancellationToken: ct).ConfigureAwait(true);
        using (document)
        {
            var deviceId = document.RootElement.GetProperty("deviceId").GetString()!;
            var older = document.RootElement.GetProperty("credential").GetString()!;
            var issuer = host.Services.GetRequiredService<DeviceCredentialIssuer>();
            var store = host.Services.GetRequiredService<IDispatchStore>();
            var row = await store.GetDeviceAsync("acct", deviceId, ct).ConfigureAwait(true);
            var rotated = await issuer.RotateAsync(store, deviceId, "acct", DeviceCredentialIssuer.Thumbprint(row!.PublicKey!.Split("\"x\":\"")[1].Split('"')[0], row.PublicKey.Split("\"y\":\"")[1].Split('"')[0]), ct).ConfigureAwait(true);
            rotated.ShouldNotBeNull();
            using var connect = new HttpRequestMessage(HttpMethod.Get, $"/dispatch/devices/{deviceId}/connect");
            connect.Headers.Authorization = new AuthenticationHeaderValue("Bearer", older);
            using var refused = await client.SendAsync(connect, ct).ConfigureAwait(true);
            refused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            using var current = new HttpRequestMessage(HttpMethod.Get, $"/dispatch/devices/{deviceId}/connect");
            current.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rotated.Token);
            using var accepted = await client.SendAsync(current, ct).ConfigureAwait(true);
            accepted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task An_access_token_is_refused_on_connect_and_a_credential_is_refused_on_pairing()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = Client(host, fresh: true);
        using var paired = await DispatchApiHost.PostJsonAsync(client, "/dispatch/devices", BodyJson(), ct).ConfigureAwait(true);
        var credential = (await JsonDocument.ParseAsync(await paired.Content.ReadAsStreamAsync(ct).ConfigureAwait(true), cancellationToken: ct).ConfigureAwait(true)).RootElement.GetProperty("credential").GetString();

        using var connect = new HttpRequestMessage(HttpMethod.Get, "/dispatch/devices/missing/connect");
        connect.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "access-token");
        using var access = await client.SendAsync(connect, ct).ConfigureAwait(true);
        access.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Remove(DispatchTestAuthHandler.SubjectHeader);
        using var other = new HttpRequestMessage(HttpMethod.Post, "/dispatch/devices") { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        other.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        using var refused = await client.SendAsync(other, ct).ConfigureAwait(true);
        refused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_own_claim_outside_AllowedSubjects_is_403_on_connect()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = Client(host, fresh: true);
        using var paired = await DispatchApiHost.PostJsonAsync(client, "/dispatch/devices", BodyJson(), ct).ConfigureAwait(true);
        var document = await JsonDocument.ParseAsync(await paired.Content.ReadAsStreamAsync(ct).ConfigureAwait(true), cancellationToken: ct).ConfigureAwait(true);
        var deviceId = document.RootElement.GetProperty("deviceId").GetString();
        var credential = document.RootElement.GetProperty("credential").GetString();
        host.Auth.AllowedSubjects.Clear();
        host.Auth.AllowedSubjects.Add("someone-else");

        using var connect = new HttpRequestMessage(HttpMethod.Get, $"/dispatch/devices/{deviceId}/connect");
        connect.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        using var response = await client.SendAsync(connect, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true)).ShouldContain("subject_not_allowed", Case.Sensitive);
    }

    private static HttpClient Client(DispatchApiHost host, bool fresh)
    {
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.SubjectHeader, "acct");
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.SessionHeader, "session-1");
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(fresh ? -1 : -11).ToUnixTimeSeconds();
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.AmrHeader, DispatchTestAuthHandler.Amr(fresh ? "password" : "token_refresh", timestamp));
        if (!fresh)
        {
            client.DefaultRequestHeaders.Remove(DispatchTestAuthHandler.AmrHeader);
            client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.AmrHeader, DispatchTestAuthHandler.Amr("password", timestamp));
        }

        return client;
    }

    private static string BodyJson()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var x = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(parameters.Q.X);
        var y = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(parameters.Q.Y);
        return "{\"name\":\" Desk \",\"platform\":\"linux\",\"appVersion\":\"1\",\"protocolVersions\":[\"1\"],\"capabilities\":[{\"id\":\"browser_use\",\"version\":1,\"enabled\":true}],\"publicKey\":{\"kty\":\"EC\",\"crv\":\"P-256\",\"x\":\"" + x + "\",\"y\":\"" + y + "\"}}";
    }
}
