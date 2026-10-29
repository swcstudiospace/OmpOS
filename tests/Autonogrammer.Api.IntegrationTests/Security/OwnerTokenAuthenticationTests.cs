using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>Owner-token authentication: which Authorization headers are accepted, and that nothing echoes a token.</summary>
public sealed class OwnerTokenAuthenticationTests
{
    /// <summary>Verifies the configured token authenticates and its subject owns the sessions it creates.</summary>
    [Fact]
    public async Task CorrectToken_IsAcceptedAndSubjectOwnsSession()
    {
        using var factory = OwnerTokenTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        OwnerTokenTestHost.UseBearer(client, OwnerTokenTestHost.Token);

        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);

        session.OwnerId.ShouldBe("operator");
    }

    /// <summary>Verifies each malformed or wrong credential answers 401 with a Bearer challenge.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("Bearer wrong-token")]
    [InlineData("Bearer owner-token-for-tests-0123456789abcdefghijklmnopqrstuvwxy")]
    [InlineData("Bearer owner-token-for-tests-0123456789abcdefghijklmnopqrstuvwxyz-extra")]
    [InlineData("Bearer owner-token-for-tests 0123456789abcdefghijklmnopqrstuvwxyz")]
    [InlineData("Bearer a")]
    [InlineData("Basic b3duZXI6dG9rZW4=")]
    public async Task WrongOrMalformedCredential_Returns401WithBearerChallenge(string? authorization)
    {
        using var factory = OwnerTokenTestHost.CreateFactory();
        using var client = factory.CreateClient();

        using var response = await OwnerTokenTestHost.GetSessionsAsync(client, authorization, null, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldContain("Bearer");
    }

    /// <summary>Verifies the scheme name of the header is case-insensitive.</summary>
    [Theory]
    [InlineData("bearer")]
    [InlineData("BEARER")]
    public async Task SchemeCase_IsIgnored(string scheme)
    {
        using var factory = OwnerTokenTestHost.CreateFactory();
        using var client = factory.CreateClient();

        using var response = await OwnerTokenTestHost.GetSessionsAsync(client, $"{scheme} {OwnerTokenTestHost.Token}", null, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies a token in the query string is never accepted.</summary>
    [Fact]
    public async Task TokenInQueryString_IsNotAccepted()
    {
        using var factory = OwnerTokenTestHost.CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"/sessions?access_token={OwnerTokenTestHost.Token}", UriKind.Relative),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Verifies a second configured digest is accepted, so the token can be rotated without downtime.</summary>
    [Fact]
    public async Task RotatedDigest_IsAcceptedAlongsideTheFirst()
    {
        var config = OwnerTokenTestHost.Config();
        config["Api:Auth:OwnerToken:Sha256:1"] = OwnerTokenTestHost.Digest(OwnerTokenTestHost.RotatedToken);
        using var factory = OwnerTokenTestHost.CreateFactory(config);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var first = await OwnerTokenTestHost.GetSessionsAsync(client, $"Bearer {OwnerTokenTestHost.Token}", null, testToken).ConfigureAwait(true);
        using var second = await OwnerTokenTestHost.GetSessionsAsync(client, $"Bearer {OwnerTokenTestHost.RotatedToken}", null, testToken).ConfigureAwait(true);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies neither the logs nor the response of a rejected attempt contain the presented token.</summary>
    [Fact]
    public async Task RejectedAttempt_NeverEchoesTheTokenInLogsOrBody()
    {
        const string presented = "presented-secret-token-value-0123456789";
        using var logs = new CapturingLoggerProvider();
        using var factory = OwnerTokenTestHost.CreateFactory(logs: logs);
        using var client = factory.CreateClient();

        using var response = await OwnerTokenTestHost.GetSessionsAsync(client, $"Bearer {presented}", "203.0.113.9", TestContext.Current.CancellationToken).ConfigureAwait(true);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        body.ShouldNotContain(presented);
        logs.Messages.ShouldNotBeEmpty();
        logs.Messages.ShouldAllBe(static message => !message.Contains(presented, StringComparison.Ordinal));
        logs.Messages.ShouldAllBe(static message => !message.Contains("203.0.113.9", StringComparison.Ordinal));
    }
}
