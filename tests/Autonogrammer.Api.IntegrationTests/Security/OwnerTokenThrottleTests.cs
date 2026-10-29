using System.Net;
using Autonogrammer.Api.Security;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>Failed owner-token attempts are throttled per client address; correct tokens never are.</summary>
public sealed class OwnerTokenThrottleTests
{
    /// <summary>Verifies attempts beyond the budget answer 429 with Retry-After, while a correct token from the same address still works.</summary>
    [Fact]
    public async Task FailuresBeyondBudget_Return429WithRetryAfter_ButCorrectTokenStillWorks()
    {
        using var factory = OwnerTokenTestHost.CreateFactory(OwnerTokenTestHost.Config(maxFailures: 2));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var rejected = await OwnerTokenTestHost.GetSessionsAsync(client, "Bearer wrong", "198.51.100.1", testToken).ConfigureAwait(true);
            rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var throttled = await OwnerTokenTestHost.GetSessionsAsync(client, "Bearer wrong", "198.51.100.1", testToken).ConfigureAwait(true);
        using var accepted = await OwnerTokenTestHost.GetSessionsAsync(client, $"Bearer {OwnerTokenTestHost.Token}", "198.51.100.1", testToken).ConfigureAwait(true);

        throttled.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter.ShouldNotBeNull();
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies one address being throttled does not affect another address.</summary>
    [Fact]
    public async Task ThrottledAddress_DoesNotAffectAnotherAddress()
    {
        using var factory = OwnerTokenTestHost.CreateFactory(OwnerTokenTestHost.Config(maxFailures: 1));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var first = await OwnerTokenTestHost.GetSessionsAsync(client, "Bearer wrong", "198.51.100.1", testToken).ConfigureAwait(true);
        using var second = await OwnerTokenTestHost.GetSessionsAsync(client, "Bearer wrong", "198.51.100.1", testToken).ConfigureAwait(true);
        using var other = await OwnerTokenTestHost.GetSessionsAsync(client, "Bearer wrong", "198.51.100.2", testToken).ConfigureAwait(true);

        first.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        other.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Verifies the window resets with the clock and the table stays bounded.</summary>
    [Fact]
    public void Limiter_ResetsAfterTheWindow_AndFailsClosedWhenFull()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var limiter = new OwnerTokenFailureLimiter(clock);

        limiter.RegisterFailure("a", 1).ShouldBeNull();
        limiter.RegisterFailure("a", 1).ShouldNotBeNull();
        clock.Advance(TimeSpan.FromSeconds(61));
        limiter.RegisterFailure("a", 1).ShouldBeNull();

        for (var i = 0; i < OwnerTokenFailureLimiter.MaxKeys; i++)
        {
            limiter.RegisterFailure("k" + i, 5);
        }

        limiter.RegisterFailure("overflow", 5).ShouldNotBeNull();
        clock.Advance(TimeSpan.FromSeconds(61));
        limiter.RegisterFailure("overflow", 5).ShouldBeNull();
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public MutableTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
