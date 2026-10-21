using System.Net;
using System.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

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
