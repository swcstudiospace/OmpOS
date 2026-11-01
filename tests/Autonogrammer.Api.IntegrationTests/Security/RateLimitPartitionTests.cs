using System.Net;
using System.Reflection;
using Autonogrammer.Api.Endpoints;
using Autonogrammer.Api.IntegrationTests.Cowork;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>Per-caller rate-limit partitions: subjects, addresses and the bounded key.</summary>
public sealed class RateLimitPartitionTests
{
    /// <summary>Keys of the keyed registrations whose key is not the policy's own <c>PolicyName</c> constant.</summary>
    private static readonly Dictionary<Type, string> RegisteredKeys = new Dictionary<Type, string>
    {
        [typeof(SessionRateLimiterPolicy)] = SessionEndpoints.PromptsPolicy,
        [typeof(SessionCreationRateLimiterPolicy)] = SessionEndpoints.CreationPolicy,
        [typeof(ProviderRateLimiterPolicy)] = ProvidersEndpoints.RateLimitPolicy,
    };

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

    /// <summary>Verifies the account budget is per subject: alice's second read is refused while bob's first read is admitted.</summary>
    [Fact]
    public async Task AccountPolicy_PartitionsBySubject()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Accounts:RequestsPerMinute"] = "1";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var alice = AuthenticatedClient(factory, "alice");
        using var bob = AuthenticatedClient(factory, "bob");

        await AssertIndependentBudgetsAsync(
            alice,
            bob,
            static (client, cancellationToken) => client.GetAsync(new Uri("/account", UriKind.Relative), cancellationToken),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Verifies the provider-catalogue budget is per subject, through the same real limiter as the other per-caller policies.</summary>
    [Fact]
    public async Task ProvidersPolicy_PartitionsBySubject()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Providers:RequestsPerMinute"] = "1";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var alice = AuthenticatedClient(factory, "alice");
        using var bob = AuthenticatedClient(factory, "bob");

        await AssertIndependentBudgetsAsync(
            alice,
            bob,
            static (client, cancellationToken) => client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), cancellationToken),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Verifies the Cowork project-route budget is per subject.</summary>
    [Fact]
    public async Task CoworkProjectsPolicy_PartitionsBySubject()
    {
        var host = await CoworkModuleHost.StartAsync(projects: true, memory: false, projectsPerMinute: 1).ConfigureAwait(true);
        try
        {
            using var alice = host.Client("alice");
            using var bob = host.Client("bob");

            await AssertIndependentBudgetsAsync(
                alice,
                bob,
                static (client, cancellationToken) => client.GetAsync(new Uri("/cowork/projects", UriKind.Relative), cancellationToken),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Verifies the prompt budget is per subject: alice's prompts draw on her own budget, so bob's prompt to the same route is still admitted.</summary>
    [Fact]
    public async Task PromptPolicy_PartitionsBySubject()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Api:RateLimit:PromptsPerMinute"] = "1";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var alice = AuthenticatedClient(factory, "alice");
        using var bob = AuthenticatedClient(factory, "bob");
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(alice, testToken).ConfigureAwait(true);

        await AssertIndependentBudgetsAsync(
            alice,
            bob,
            (client, cancellationToken) => SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"one\"}", cancellationToken),
            testToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Verifies every rate-limit policy in the API assembly, discovered by type rather than listed by hand, keys two callers apart: alice
    /// (subject alice, address 10.0.0.1) and bob (subject bob, address 10.0.0.2) never share a partition, so no policy shares one budget between them.
    /// </summary>
    [Fact]
    public void EveryDiscoveredPolicy_PartitionsEachCallerIndependently()
    {
        using var factory = new ApiFactory(JwtTestHost.DevelopmentConfig(), JwtTestHost.InjectStaticMetadata);

        foreach (var (type, policy) in DiscoveredPolicies(factory.Services))
        {
            var alice = policy.GetPartition(CallerContext("alice", "10.0.0.1")).PartitionKey;
            var bob = policy.GetPartition(CallerContext("bob", "10.0.0.2")).PartitionKey;

            alice.ShouldNotBe(bob, StringComparer.Ordinal, $"{type.FullName} gives alice and bob the same partition key '{alice}'");
        }
    }

    /// <summary>
    /// Verifies every rate-limit policy in the API assembly keys the same caller the same way on a second call, so a caller's bucket is found
    /// again rather than replaced.
    /// </summary>
    [Fact]
    public void EveryDiscoveredPolicy_KeysTheSameCallerTheSameWay()
    {
        using var factory = new ApiFactory(JwtTestHost.DevelopmentConfig(), JwtTestHost.InjectStaticMetadata);

        foreach (var (type, policy) in DiscoveredPolicies(factory.Services))
        {
            var first = policy.GetPartition(CallerContext("alice", "10.0.0.1")).PartitionKey;
            var second = policy.GetPartition(CallerContext("alice", "10.0.0.1")).PartitionKey;

            second.ShouldBe(first, $"{type.FullName} keyed alice differently on a second call");
        }
    }

    /// <summary>
    /// Subject-keyed policies give two subjects on one address two partitions, and one subject on two addresses one partition. Address-keyed policies
    /// do the reverse. The address-keyed policies are named here, each name must be a discovered policy, and every other discovered policy is checked
    /// as subject-keyed, so a policy that changes its key fails this test.
    /// </summary>
    [Fact]
    public void EveryDiscoveredPolicy_KeysBySubjectOrAddressAsDocumented()
    {
        using var factory = new ApiFactory(JwtTestHost.DevelopmentConfig(), JwtTestHost.InjectStaticMetadata);
        var discovered = DiscoveredPolicies(factory.Services);
        string[] addressKeyed = ["AuthConfigRateLimiterPolicy", "AutomationWebhookRateLimiterPolicy", "ArtifactRenderRateLimiterPolicy", "DispatchConnectRateLimiterPolicy"];
        foreach (var name in addressKeyed)
        {
            discovered.ShouldContain(entry => entry.PolicyType.Name == name, $"{name} is named as address-keyed but was not discovered");
        }

        foreach (var (type, policy) in discovered)
        {
            var alice = policy.GetPartition(CallerContext("alice", "10.0.0.1")).PartitionKey;
            var bob = policy.GetPartition(CallerContext("bob", "10.0.0.1")).PartitionKey;
            var aliceElsewhere = policy.GetPartition(CallerContext("alice", "10.0.0.2")).PartitionKey;
            if (addressKeyed.Contains(type.Name, StringComparer.Ordinal))
            {
                bob.ShouldBe(alice, $"{type.FullName} is address-keyed, so two subjects on one address share a partition");
                string.Equals(aliceElsewhere, alice, StringComparison.Ordinal).ShouldBeFalse($"{type.FullName} is address-keyed, so one subject on two addresses gets two partitions");
            }
            else
            {
                string.Equals(bob, alice, StringComparison.Ordinal).ShouldBeFalse($"{type.FullName} is subject-keyed, so two subjects on one address get two partitions");
                aliceElsewhere.ShouldBe(alice, $"{type.FullName} is subject-keyed, so one subject keeps one partition across addresses");
            }
        }
    }

    /// <summary>Creates a client that presents the subject's bearer token.</summary>
    /// <param name="factory">The test host.</param>
    /// <param name="subject">The token subject.</param>
    /// <returns>The authenticated client (caller disposes).</returns>
    private static HttpClient AuthenticatedClient(ApiFactory factory, string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken(subject));
        return client;
    }

    /// <summary>
    /// Sends the same request as alice, then as bob, then as alice again, under a budget of one per caller. Alice's first and bob's first requests
    /// are admitted; alice's second is refused. Bob's budget is his own, not the one alice spent.
    /// </summary>
    /// <param name="alice">Alice's client.</param>
    /// <param name="bob">Bob's client.</param>
    /// <param name="send">Sends one request through the given client.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    private static async Task AssertIndependentBudgetsAsync(
        HttpClient alice,
        HttpClient bob,
        Func<HttpClient, CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        using var aliceFirst = await send(alice, cancellationToken).ConfigureAwait(false);
        using var bobFirst = await send(bob, cancellationToken).ConfigureAwait(false);
        using var aliceSecond = await send(alice, cancellationToken).ConfigureAwait(false);

        aliceFirst.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests, "alice's first request is inside her budget");
        bobFirst.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests, "bob's budget is his own, not the one alice spent");
        aliceSecond.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests, "alice's second request exceeds her budget of one");
    }

    private static async Task<HttpResponseMessage> GetAuthConfigAsync(HttpClient client, string address, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/auth/config", UriKind.Relative));
        request.Headers.TryAddWithoutValidation(OwnerTokenTestHost.RemoteAddressHeader, address);
        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Finds every concrete <see cref="IRateLimiterPolicy{TKey}"/> of string keys in the API assembly, nested types included, and resolves each one.</summary>
    /// <param name="services">The test host's services.</param>
    /// <returns>Each discovered policy type with a constructed instance.</returns>
    private static (Type PolicyType, IRateLimiterPolicy<string> Policy)[] DiscoveredPolicies(IServiceProvider services)
    {
        var policies = DiscoverPolicyTypes().Select(type => (PolicyType: type, Policy: ResolvePolicy(services, type))).ToArray();
        policies.ShouldNotBeEmpty("no IRateLimiterPolicy<string> type was discovered in the API assembly");
        return policies;
    }

    private static Type[] DiscoverPolicyTypes()
    {
        var policyInterface = typeof(IRateLimiterPolicy<string>);
        return WithNestedTypes(typeof(RateLimitPartitionKey).Assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } && policyInterface.IsAssignableFrom(type))
            .Distinct()
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<Type> WithNestedTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in WithNestedTypes(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)))
            {
                yield return nested;
            }
        }
    }

    /// <summary>Gets the instance the host registered under the policy's key, or constructs one from the host's services when none is registered.</summary>
    /// <param name="services">The test host's services.</param>
    /// <param name="type">The policy type.</param>
    /// <returns>The policy instance.</returns>
    private static IRateLimiterPolicy<string> ResolvePolicy(IServiceProvider services, Type type)
    {
        var key = RegisteredKey(type);
        if (key is not null
            && services.GetKeyedService<IRateLimiterPolicy<string>>(key) is { } registered
            && type.IsInstanceOfType(registered))
        {
            return registered;
        }

        try
        {
            return (IRateLimiterPolicy<string>)ActivatorUtilities.CreateInstance(services, type);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{type.FullName} could not be constructed from the test host's services: {exception.Message}", exception);
        }
    }

    private static string? RegisteredKey(Type type) =>
        RegisteredKeys.TryGetValue(type, out var registeredKey)
            ? registeredKey
            : type.GetField("PolicyName", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string;

    /// <summary>Builds a caller with a subject, a remote address and the device id the connect route reads. The device id is the same for every caller, so only the subject or the address tells them apart.</summary>
    /// <param name="subject">The token subject.</param>
    /// <param name="address">The remote address.</param>
    /// <returns>The caller's context.</returns>
    private static DefaultHttpContext CallerContext(string subject, string address)
    {
        var context = Context(subject, address);
        context.Request.RouteValues["id"] = "device-1";
        return context;
    }
}
