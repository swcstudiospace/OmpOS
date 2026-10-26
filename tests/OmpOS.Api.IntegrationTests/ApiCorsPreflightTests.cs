using System.Net;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// CORS lock (Phase 12 criterion 3): with Api:Cors:Origins empty, unauthenticated cross-origin requests get no
/// Access-Control-Allow-Origin header; a listed origin is allowed.
/// </summary>
public sealed class ApiCorsPreflightTests
{
    /// <summary>Verifies a preflight and an unauthenticated request from an unlisted origin receive no cross-origin grant.</summary>
    [Fact]
    public async Task CorsPreflight_UnlistedOrigin_IsDenied()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var preflight = Preflight("https://evil.test.invalid");
        using var preflightResponse = await client.SendAsync(preflight, testToken).ConfigureAwait(true);
        preflightResponse.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();

        using var actual = new HttpRequestMessage(HttpMethod.Get, new Uri("/sessions", UriKind.Relative));
        actual.Headers.Add("Origin", "https://evil.test.invalid");
        using var actualResponse = await client.SendAsync(actual, testToken).ConfigureAwait(true);
        actualResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        actualResponse.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    /// <summary>Verifies a preflight from a listed origin is granted, proving the denial above is the origin check and not a broken pipeline.</summary>
    [Fact]
    public async Task CorsPreflight_ListedOrigin_IsAllowed()
    {
        using var factory = new OmpApiFactory(
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Cors:Origins:0"] = "https://app.test.invalid" });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var preflight = Preflight("https://app.test.invalid");
        using var preflightResponse = await client.SendAsync(preflight, testToken).ConfigureAwait(true);

        preflightResponse.Headers.GetValues("Access-Control-Allow-Origin")
            .Contains("https://app.test.invalid", StringComparer.Ordinal)
            .ShouldBeTrue();
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/sessions", UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return request;
    }
}
