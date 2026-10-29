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

    /// <summary>
    /// Verifies two unauthenticated callers behind different remote addresses get independent budgets and one address shares its own: there is
    /// no single bucket for every unauthenticated caller (D-47).
    /// </summary>
    [Fact]
    public async Task TwoUnauthenticatedAddresses_DoNotShareABudget_AndOneAddressSharesItsOwn()
    {
        using var factory = OwnerTokenTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        var firstStatuses = new List<HttpStatusCode>();
        for (var request = 0; request <= AuthConfigRateLimiterPolicy.RequestsPerMinute; request++)
        {
            using var response = await GetAuthConfigAsync(client, "203.0.113.10", testToken).ConfigureAwait(true);
            firstStatuses.Add(response.StatusCode);
        }

        using var otherAddress = await GetAuthConfigAsync(client, "203.0.113.11", testToken).ConfigureAwait(true);

        firstStatuses.Take(AuthConfigRateLimiterPolicy.RequestsPerMinute).ShouldAllBe(static status => status == HttpStatusCode.OK);
        firstStatuses[^1].ShouldBe(HttpStatusCode.TooManyRequests);
        otherAddress.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies every per-caller policy partitions through <see cref="RateLimitPartitionKey.For"/>, so a policy that buckets on a constant
    /// key (one shared budget for everyone) fails. The auth-config and automation-webhook policies are anonymous endpoints and use the address
    /// key by design.
    /// </summary>
    [Fact]
    public void EveryPerCallerPolicy_PartitionsThroughTheCallerKey()
    {
        string[] addressPolicies = [nameof(AuthConfigRateLimiterPolicy), "AutomationWebhookRateLimiterPolicy"];
        var policies = typeof(RateLimitPartitionKey).Assembly.GetTypes()
            .Where(static type => type is { IsClass: true, IsAbstract: false } && type.GetInterfaces().Contains(typeof(Microsoft.AspNetCore.RateLimiting.IRateLimiterPolicy<string>)))
            .Select(static type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var root = FindRepoRoot();
        var apiSources = Directory.EnumerateFiles(Path.Combine(root, "services", "Autonogrammer.Api"), "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        policies.Length.ShouldBeGreaterThanOrEqualTo(7);
        foreach (var policy in policies)
        {
            var file = apiSources.SingleOrDefault(path => string.Equals(Path.GetFileName(path), policy + ".cs", StringComparison.Ordinal));
            file.ShouldNotBeNull($"no source file named {policy}.cs");
            var source = StripComments(File.ReadAllText(file));
            var expected = addressPolicies.Contains(policy, StringComparer.Ordinal) ? "RateLimitPartitionKey.ForAddress(" : "RateLimitPartitionKey.For(";
            source.ShouldContain(expected, Case.Sensitive, $"{policy} must partition through {expected}");
        }
    }

    private static async Task<HttpResponseMessage> GetAuthConfigAsync(HttpClient client, string address, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/auth/config", UriKind.Relative));
        request.Headers.TryAddWithoutValidation(OwnerTokenTestHost.RemoteAddressHeader, address);
        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string StripComments(string source) =>
        System.Text.RegularExpressions.Regex.Replace(source, @"//[^\r\n]*|/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline, TimeSpan.FromSeconds(5));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate Autonogrammer.slnx walking up from " + AppContext.BaseDirectory);
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
