using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Autonogrammer.Api.Accounts;
using Autonogrammer.Api.IntegrationTests.Hosting;
using Autonogrammer.Api.IntegrationTests.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        config["Accounts:Enabled"] = "true";
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

    /// <summary>
    /// Two first requests create no row while the store is down, a failure never fails the request, the next account request creates one row that
    /// <c>GET /account</c> already sees, later sightings wait ten minutes, and the move after ten minutes lands.
    /// </summary>
    [Fact]
    public async Task Enrollment_IsIdempotent_Cached_AndNonBlocking()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider();
        var failing = new FailingAccountStore();
        using var factory = OidcFactory(clock, failing);
        using var client = AliceClient(factory);

        var first = client.GetAsync(new Uri("/sessions", UriKind.Relative), ct);
        var second = client.GetAsync(new Uri("/sessions", UriKind.Relative), ct);
        using var left = await first.ConfigureAwait(true);
        using var right = await second.ConfigureAwait(true);
        left.StatusCode.ShouldBe(HttpStatusCode.OK);
        right.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await failing.CountAsync(ct).ConfigureAwait(true)).ShouldBe(0);

        // An account request waits for its own write, so once the store is back a few requests at most leave the row visible on the response.
        failing.Fail = false;
        (await EventuallyAsync(() => AccountHasRowAsync(client, ct), static seen => seen, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await failing.CountAsync(ct).ConfigureAwait(true)).ShouldBe(1);
        var row = await failing.FindAsync("alice", ct).ConfigureAwait(true);
        row.ShouldNotBeNull();
        row.CreatedAt.ShouldBe(clock.GetUtcNow());

        clock.Advance(TimeSpan.FromMinutes(9));
        (await AccountHasRowAsync(client, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await failing.FindAsync("alice", ct).ConfigureAwait(true))!.LastSeenAt.ShouldBe(row.LastSeenAt);

        clock.Advance(TimeSpan.FromMinutes(1));
        (await AccountHasRowAsync(client, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await failing.FindAsync("alice", ct).ConfigureAwait(true))!.LastSeenAt.ShouldBe(clock.GetUtcNow());
    }

    /// <summary>A route that does not read the account row answers while the store write is still blocked, and the write lands once the store answers.</summary>
    [Fact]
    public async Task NonAccountRoute_DoesNotWaitForTheStoreWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedAccountStore();
        using var factory = OidcFactory(new ManualTimeProvider(), store);
        using var client = AliceClient(factory);

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await store.WaitStartedAsync(ct).ConfigureAwait(true);
        (await store.CountAsync(ct).ConfigureAwait(true)).ShouldBe(0);

        store.Open();
        (await EventuallyAsync(() => store.CountAsync(ct), static count => count == 1, ct).ConfigureAwait(true)).ShouldBe(1);
    }

    /// <summary>Concurrent first requests of one caller share one pending write.</summary>
    [Fact]
    public async Task ConcurrentFirstRequests_ShareOnePendingWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedAccountStore();
        using var factory = OidcFactory(new ManualTimeProvider(), store);
        using var client = AliceClient(factory);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.GetAsync(new Uri("/sessions", UriKind.Relative), ct))).ConfigureAwait(true);
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Dispose();
        }

        await store.WaitStartedAsync(ct).ConfigureAwait(true);
        store.Calls.ShouldBe(1);
        store.Open();
        (await EventuallyAsync(() => store.CountAsync(ct), static count => count == 1, ct).ConfigureAwait(true)).ShouldBe(1);
        store.Calls.ShouldBe(1);
    }

    /// <summary>An account route waits for the caller's write only for the bounded time and then answers without the row.</summary>
    [Fact]
    public async Task AccountRoute_WaitIsBounded()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider();
        var store = new GatedAccountStore();
        using var factory = OidcFactory(clock, store);
        var client = AliceClient(factory);
        try
        {
            var request = AccountHasRowAsync(client, ct);
            await store.WaitStartedAsync(ct).ConfigureAwait(true);
            while (!request.IsCompleted)
            {
                clock.Advance(TimeSpan.FromSeconds(2.5));
                await Task.Delay(TimeSpan.FromMilliseconds(20), ct).ConfigureAwait(true);
            }

            (await request.ConfigureAwait(true)).ShouldBeFalse();
        }
        finally
        {
            client.Dispose();
        }

        store.Open();
        (await EventuallyAsync(() => store.CountAsync(ct), static count => count == 1, ct).ConfigureAwait(true)).ShouldBe(1);
    }

    /// <summary>A write that outlives its timeout is cancelled, and the next request enrolls the caller again.</summary>
    [Fact]
    public async Task WriteTimeout_CancelsTheWrite_AndTheNextRequestRetries()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider();
        var store = new GatedAccountStore();
        using var factory = OidcFactory(clock, store);
        using var client = AliceClient(factory);

        using var first = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        await store.WaitStartedAsync(ct).ConfigureAwait(true);
        clock.Advance(AccountEnrollmentMiddleware.WriteTimeout + TimeSpan.FromSeconds(1));
        await store.WaitCancelledAsync(ct).ConfigureAwait(true);

        store.Open();
        _ = await EventuallyAsync(
            async () =>
            {
                using var again = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(false);
                return await store.CountAsync(ct).ConfigureAwait(false);
            },
            static count => count == 1,
            ct).ConfigureAwait(true);
        (await store.CountAsync(ct).ConfigureAwait(true)).ShouldBe(1);
        store.Calls.ShouldBeGreaterThanOrEqualTo(2);
    }

    /// <summary>A write still pending when the host begins stopping is cancelled instead of outliving the host.</summary>
    [Fact]
    public async Task HostStopping_CancelsAPendingWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedAccountStore();
        using var factory = OidcFactory(new ManualTimeProvider(), store);
        using var client = AliceClient(factory);

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        await store.WaitStartedAsync(ct).ConfigureAwait(true);

        factory.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        await store.WaitCancelledAsync(ct).ConfigureAwait(true);
        (await store.CountAsync(ct).ConfigureAwait(true)).ShouldBe(0);
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

    private static async Task<bool> AccountHasRowAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(new Uri("/account", UriKind.Relative), ct).ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(false);
        return body.RootElement.TryGetProperty("createdAt", out var created) && created.ValueKind != JsonValueKind.Null;
    }

    private static async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> done, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            var value = await read().ConfigureAwait(false);
            if (done(value) || DateTime.UtcNow > deadline)
            {
                return value;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), ct).ConfigureAwait(false);
        }
    }

    private static ApiFactory OidcFactory(ManualTimeProvider clock, IAccountStore store)
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Accounts:Enabled"] = "true";
        return new ApiFactory(
            config,
            services =>
            {
                JwtTestHost.InjectStaticMetadata(services);
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IAccountStore>(store);
            });
    }

    private static HttpClient AliceClient(ApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        return client;
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

    /// <summary>A store whose writes block until the test opens the gate, and that records calls and cancellation.</summary>
    private sealed class GatedAccountStore : IAccountStore
    {
        private readonly InMemoryAccountStore _inner = new();
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task WaitStartedAsync(CancellationToken cancellationToken) => WaitAsync(_started, cancellationToken);

        public Task WaitCancelledAsync(CancellationToken cancellationToken) => WaitAsync(_cancelled, cancellationToken);

        public void Open() => _ = _gate.TrySetResult();

        private static async Task WaitAsync(TaskCompletionSource signal, CancellationToken cancellationToken) =>
            await signal.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);

        public async Task TouchAsync(string id, DateTimeOffset seenAt, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _calls);
            _ = _started.TrySetResult();
            try
            {
                await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _ = _cancelled.TrySetResult();
                throw;
            }

            await _inner.TouchAsync(id, seenAt, cancellationToken).ConfigureAwait(false);
        }

        public Task<AccountRow?> FindAsync(string id, CancellationToken cancellationToken) => _inner.FindAsync(id, cancellationToken);

        public Task<int> CountAsync(CancellationToken cancellationToken) => _inner.CountAsync(cancellationToken);
    }
}
