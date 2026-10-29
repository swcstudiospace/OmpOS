using System.Net;
using Autonogrammer.Api.Security;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>Per-caller rate-limit partitions: subjects, addresses and the bounded key.</summary>
public sealed class RateLimitPartitionTests
{
    /// <summary>Verifies the key is the subject when authenticated, the address otherwise, and anonymous without either.</summary>
    [Fact]
    public void Key_PrefersSubject_ThenAddress_ThenAnonymous()
    {
        var withSubject = Context("alice", "203.0.113.5");
        var addressOnly = Context(null, "203.0.113.5");
        var neither = Context(null, null);

        RateLimitPartitionKey.For(withSubject).ShouldBe("sub:alice");
        RateLimitPartitionKey.For(addressOnly).ShouldBe("ip:203.0.113.5");
        RateLimitPartitionKey.For(neither).ShouldBe(RateLimitPartitionKey.Anonymous);
        RateLimitPartitionKey.ForAddress(withSubject).ShouldBe("ip:203.0.113.5");
    }

    /// <summary>Verifies an oversized subject is truncated to the bound.</summary>
    [Fact]
    public void Key_IsBounded() =>
        RateLimitPartitionKey.For(Context(new string('x', 10_000), null)).Length.ShouldBe(RateLimitPartitionKey.MaxLength);

    /// <summary>Verifies one subject exhausting its prompt budget does not use up another subject's budget.</summary>
    [Fact]
    public async Task TwoSubjects_DoNotShareABudget_AndOneExhaustsItsOwn()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Api:RateLimit:SessionsPerMinute"] = "1";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var alice = factory.CreateClient();
        using var bob = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        alice.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        bob.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("bob"));

        using var aliceFirst = await alice.PostAsync(new Uri("/sessions", UriKind.Relative), null, testToken).ConfigureAwait(true);
        using var aliceSecond = await alice.PostAsync(new Uri("/sessions", UriKind.Relative), null, testToken).ConfigureAwait(true);
        using var bobFirst = await bob.PostAsync(new Uri("/sessions", UriKind.Relative), null, testToken).ConfigureAwait(true);

        aliceFirst.StatusCode.ShouldBe(HttpStatusCode.Created);
        aliceSecond.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        bobFirst.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static DefaultHttpContext Context(string? subject, string? address)
    {
        var context = new DefaultHttpContext();
        if (subject is not null)
        {
            context.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", subject)], "test"));
        }

        if (address is not null)
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        }

        return context;
    }
}
