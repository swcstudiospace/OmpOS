using Autonogrammer.Api.IntegrationTests.Security;
using System.Net;
using System.Net.Http.Headers;
using Autonogrammer.Api.Accounts;
using Autonogrammer.Api.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Accounts;

/// <summary>Account enrollment, the ten-minute sighting cache and the accounts limiter.</summary>
public sealed class AccountEnrollmentTests
{
    /// <summary>The accounts budget defaults to 120 and an out-of-range value fails startup naming the key.</summary>
    [Fact]
    public async Task RequestsPerMinute_DefaultsTo120_AndRejectsZero()
    {
        using var factory = new ApiFactory(JwtTestHost.DevelopmentConfig(), JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        _ = await client.GetAsync(new Uri("/auth/config", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        factory.Services.GetRequiredService<IOptions<AccountsOptions>>().Value.RequestsPerMinute.ShouldBe(120);

        var config = JwtTestHost.DevelopmentConfig();
        config["Accounts:RequestsPerMinute"] = "0";
        using var broken = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        var error = Should.Throw<Exception>(() => broken.CreateClient());
        error.ToString().ShouldContain("Accounts:RequestsPerMinute");
    }

    /// <summary>Two subjects have independent account-route budgets.</summary>
    [Fact]
    public async Task TwoSubjects_HaveIndependentBudgets()
    {
        var ct = TestContext.Current.CancellationToken;
        var config = JwtTestHost.DevelopmentConfig();
        config["Accounts:RequestsPerMinute"] = "1";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var alice = factory.CreateClient();
        using var bob = factory.CreateClient();
        alice.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        bob.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("bob"));

        using var aliceFirst = await alice.GetAsync(new Uri("/account", UriKind.Relative), ct).ConfigureAwait(true);
        using var bobFirst = await bob.GetAsync(new Uri("/account", UriKind.Relative), ct).ConfigureAwait(true);
        using var aliceSecond = await alice.GetAsync(new Uri("/account", UriKind.Relative), ct).ConfigureAwait(true);

        aliceFirst.StatusCode.ShouldBe(HttpStatusCode.OK);
        bobFirst.StatusCode.ShouldBe(HttpStatusCode.OK);
        aliceSecond.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>Two first requests create one row, later sightings wait ten minutes, and a store failure does not fail the request.</summary>
    [Fact]
    public async Task Enrollment_IsIdempotent_Cached_AndNonBlocking()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider();
        var failing = new FailingAccountStore();
        using var factory = new ApiFactory(
            JwtTestHost.DevelopmentConfig(),
            services =>
            {
                JwtTestHost.InjectStaticMetadata(services);
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IAccountStore>(failing);
            });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));

        var first = client.GetAsync(new Uri("/sessions", UriKind.Relative), ct);
        var second = client.GetAsync(new Uri("/sessions", UriKind.Relative), ct);
        using var left = await first.ConfigureAwait(true);
        using var right = await second.ConfigureAwait(true);
        left.StatusCode.ShouldBe(HttpStatusCode.OK);
        right.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await failing.CountAsync(ct).ConfigureAwait(true)).ShouldBe(0);

        failing.Fail = false;
        using var created = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await failing.CountAsync(ct).ConfigureAwait(true)).ShouldBe(1);
        var row = await failing.FindAsync("alice", ct).ConfigureAwait(true);
        row.ShouldNotBeNull();
        row.CreatedAt.ShouldBe(clock.GetUtcNow());

        clock.Advance(TimeSpan.FromMinutes(9));
        using var cached = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        cached.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await failing.FindAsync("alice", ct).ConfigureAwait(true))!.LastSeenAt.ShouldBe(row.LastSeenAt);

        clock.Advance(TimeSpan.FromMinutes(1));
        using var later = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        later.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await failing.FindAsync("alice", ct).ConfigureAwait(true))!.LastSeenAt.ShouldBe(clock.GetUtcNow());
    }

    /// <summary>The development principal, owner-token mode and an unauthenticated request create no row.</summary>
    [Fact]
    public async Task DevelopmentOwnerTokenAndAnonymous_AreNotEnrolled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var development = new ApiFactory();
        using var devClient = development.CreateClient();
        using var devResponse = await devClient.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        devResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await development.Services.GetRequiredService<IAccountStore>().CountAsync(ct).ConfigureAwait(true)).ShouldBe(0);

        using var owner = OwnerTokenTestHost.CreateFactory();
        using var ownerClient = owner.CreateClient();
        OwnerTokenTestHost.UseBearer(ownerClient, OwnerTokenTestHost.Token);
        using var ownerResponse = await ownerClient.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        ownerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.Services.GetRequiredService<IAccountStore>().CountAsync(ct).ConfigureAwait(true)).ShouldBe(0);

        using var oidc = new ApiFactory(JwtTestHost.DevelopmentConfig(), JwtTestHost.InjectStaticMetadata);
        using var anonymous = oidc.CreateClient();
        using var denied = await anonymous.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await oidc.Services.GetRequiredService<IAccountStore>().CountAsync(ct).ConfigureAwait(true)).ShouldBe(0);
    }

    /// <summary>The startup warning names the gap and carries no configured value.</summary>
    [Fact]
    public void Startup_WarnsWhenSignupEmailHasNoCode()
    {
        var logs = new CapturingLoggerProvider();
        var config = JwtTestHost.DevelopmentConfig();
        config["Api:Auth:Client:ProjectUrl"] = "https://project.example";
        config["Api:Auth:Client:PublishableKey"] = "sb_publishable_secretish";
        config["Api:Auth:Client:MagicLinkEnabled"] = "false";
        config["Api:Auth:Client:SignupEmailHasCode"] = "false";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata, logs);
        using var client = factory.CreateClient();

        var warning = logs.Messages.Single(message => message.Contains("sign-up email has no code", StringComparison.Ordinal));
        warning.ShouldNotContain("sb_publishable_secretish");
        warning.ShouldNotContain("project.example");
    }

    private sealed class FailingAccountStore : IAccountStore
    {
        private readonly InMemoryAccountStore _inner = new();

        public bool Fail { get; set; } = true;

        public Task TouchAsync(string id, DateTimeOffset seenAt, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new InvalidOperationException("store down");
            }

            return _inner.TouchAsync(id, seenAt, cancellationToken);
        }

        public Task<AccountRow?> FindAsync(string id, CancellationToken cancellationToken) => _inner.FindAsync(id, cancellationToken);

        public Task<int> CountAsync(CancellationToken cancellationToken) => _inner.CountAsync(cancellationToken);
    }
}
