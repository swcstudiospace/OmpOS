using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Autonogrammer.Api.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Verifies disabled dispatch still maps routes and answers 404 after authorization.</summary>
public sealed class DispatchDisabledTests
{
    [Fact]
    public async Task Pairing_and_connect_answer_404_dispatch_disabled_and_stay_in_OpenAPI()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct, enabled: false).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.SubjectHeader, "acct");
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.SessionHeader, "session-1");
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.AmrHeader, DispatchTestAuthHandler.Amr("password", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

        using var pairing = await DispatchApiHost.PostJsonAsync(client, "/dispatch/devices", "{}", ct).ConfigureAwait(true);
        using var document = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), ct).ConfigureAwait(true);

        pairing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await pairing.Content.ReadAsStringAsync(ct).ConfigureAwait(true)).ShouldContain("dispatch_disabled", Case.Sensitive);
        document.StatusCode.ShouldBe(HttpStatusCode.OK);
        var openApi = await document.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        openApi.ShouldContain("/dispatch/devices", Case.Sensitive);
        openApi.ShouldContain("/connect", Case.Sensitive);
        openApi.ShouldContain("DispatchDevice", Case.Sensitive);
    }

    [Fact]
    public async Task An_authorized_connect_answers_404_when_dispatch_is_turned_off()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.SubjectHeader, "acct");
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.SessionHeader, "session-1");
        client.DefaultRequestHeaders.Add(DispatchTestAuthHandler.AmrHeader, DispatchTestAuthHandler.Amr("password", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var paired = await DispatchApiHost.PostJsonAsync(client, "/dispatch/devices", PairingJson(), ct).ConfigureAwait(true);
        paired.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var parsed = await JsonDocument.ParseAsync(await paired.Content.ReadAsStreamAsync(ct).ConfigureAwait(true), cancellationToken: ct).ConfigureAwait(true);
        host.Services.GetRequiredService<IOptions<DispatchOptions>>().Value.Enabled = false;
        using var connect = new HttpRequestMessage(HttpMethod.Get, "/dispatch/devices/" + parsed.RootElement.GetProperty("deviceId").GetString() + "/connect");
        connect.Headers.Authorization = new AuthenticationHeaderValue("Bearer", parsed.RootElement.GetProperty("credential").GetString());

        using var response = await client.SendAsync(connect, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true)).ShouldContain("dispatch_disabled", Case.Sensitive);
    }

    /// <summary>
    /// Outside Development no device store is registered. With dispatch off the device credential cannot be checked, so the connect route answers 401
    /// instead of failing while its authentication handler is built.
    /// A handler that cannot be built leaves the request pending on the test server, so this test then hangs rather than failing.
    /// </summary>
    [Fact]
    public async Task A_connect_with_dispatch_off_and_no_device_store_is_refused_as_unauthenticated()
    {
        var ct = TestContext.Current.CancellationToken;
        var config = JwtTestHost.ProductionConfig();
        config["Dispatch:Enabled"] = "false";
        using var factory = new ApiFactory(config, environment: "Production");
        using var client = factory.CreateClient();
        using var connect = new HttpRequestMessage(HttpMethod.Get, "/dispatch/devices/device-1/connect");
        connect.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-device-credential");

        using var response = await client.SendAsync(connect, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string PairingJson()
    {
        using var key = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var x = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(parameters.Q.X);
        var y = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(parameters.Q.Y);
        return "{\"name\":\"Desk\",\"platform\":\"linux\",\"appVersion\":\"1\",\"protocolVersions\":[\"1\"],\"capabilities\":[{\"id\":\"sessions\",\"version\":1,\"enabled\":true}],\"publicKey\":{\"kty\":\"EC\",\"crv\":\"P-256\",\"x\":\"" + x + "\",\"y\":\"" + y + "\"}}";
    }
}
