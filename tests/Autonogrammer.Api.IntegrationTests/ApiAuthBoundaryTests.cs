using System.Net;
using System.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// OD-03 bearer boundary on a Development host with the development principal off. Protected endpoints require a token that
/// verifies against the configured key and is issued for the configured audience; the health endpoints and the Development-only
/// OpenAPI document stay anonymous.
/// </summary>
public sealed class ApiAuthBoundaryTests
{
    /// <summary>Verifies a protected endpoint answers 401 when the request carries no bearer token.</summary>
    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Verifies a token signed by the test key but issued for another audience answers 401.</summary>
    [Fact]
    public async Task ProtectedEndpoint_TokenForWrongAudience_Returns401()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice", audience: "another-api"));

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Verifies a token with the right audience but a signature from an untrusted key answers 401.</summary>
    [Fact]
    public async Task ProtectedEndpoint_TokenSignedByUntrustedKey_Returns401()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestHost.IssueToken("mallory", signingKey: JwtTestHost.ForeignSigningKey()));

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies tokens that break the ES256, signed-token, issuer, lifetime, anonymity or role pins each answer 401. The hs256 token is signed with a key the test
    /// metadata publishes, so only the <c>ValidAlgorithms</c> pin rejects it.
    /// </summary>
    [Theory]
    [InlineData("hs256")]
    [InlineData("alg-none")]
    [InlineData("wrong-issuer")]
    [InlineData("expired-31s")]
    [InlineData("anonymous")]
    [InlineData("role-anon")]
    [InlineData("role-missing")]
    [InlineData("role-service")]
    public async Task ProtectedEndpoint_PinViolation_Returns401(string violation)
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var token = violation switch
        {
            "hs256" => JwtTestHost.IssueToken("alice", signingKey: JwtTestHost.HmacSigningKey()),
            "alg-none" => JwtTestHost.IssueUnsignedToken("alice"),
            "wrong-issuer" => JwtTestHost.IssueToken("alice", issuer: "https://other.test.invalid/"),
            "expired-31s" => JwtTestHost.IssueToken("alice", expires: DateTime.UtcNow.AddSeconds(-31)),
            "anonymous" => JwtTestHost.IssueToken("alice", anonymous: true),
            "role-anon" => JwtTestHost.IssueToken("alice", role: "anon"),
            "role-missing" => JwtTestHost.IssueToken("alice", role: null),
            _ => JwtTestHost.IssueToken("alice", role: "service_role"),
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Verifies a token that expired 10 s ago is still inside the 30 s clock skew and is accepted. The host is warmed first so a slow cold start cannot age the token past the skew.</summary>
    [Fact]
    public async Task ProtectedEndpoint_ExpiredWithinSkew_IsAccepted()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        using var warmUp = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);
        warmUp.StatusCode.ShouldBe(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice", expires: DateTime.UtcNow.AddSeconds(-10)));

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies a rotated signing key (a new kid) is picked up by a metadata refresh without a host restart.</summary>
    [Fact]
    public async Task ProtectedEndpoint_RotatedKid_RefreshesMetadataAndValidates()
    {
        var first = JwtTestHost.NewEcKey("rotation-1");
        var second = JwtTestHost.NewEcKey("rotation-2");
        var metadata = new JwtTestHost.RotatingMetadata(first);
        using var factory = new ApiFactory(JwtTestHost.DevelopmentConfig(), services => JwtTestHost.InjectRotatingMetadata(services, metadata));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice", signingKey: first));
        using var before = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);
        before.StatusCode.ShouldBe(HttpStatusCode.OK);

        metadata.Publish(second);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice", signingKey: second));
        var status = HttpStatusCode.Unauthorized;
        for (var attempt = 0; attempt < 3 && status != HttpStatusCode.OK; attempt++)
        {
            using var after = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);
            status = after.StatusCode;
        }

        status.ShouldBe(HttpStatusCode.OK);
        metadata.RefreshCount.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies a valid token gets the success response and that its subject becomes the session owner.</summary>
    [Fact]
    public async Task ProtectedEndpoint_ValidToken_ReturnsSuccessAndSubjectOwnsSession()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));

        using var list = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);

        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);
        session.OwnerId.ShouldBe("alice");
    }

    /// <summary>Verifies the liveness and readiness endpoints answer without any token.</summary>
    [Fact]
    public async Task HealthEndpoints_ReturnWithoutToken()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), testToken).ConfigureAwait(true);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies the OpenAPI document is anonymous in Development.</summary>
    [Fact]
    public async Task OpenApiDocument_IsAnonymous_InDevelopment()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var document = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), testToken).ConfigureAwait(true);

        document.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
