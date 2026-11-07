using Shouldly;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>The in-process limiter is what a rate limit degrades to when Dragonfly is down. It is never unlimited.</summary>
public sealed class InProcessRateLimiterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch + TimeSpan.FromMinutes(10);

    [Fact]
    public void TokensRunOutAtTheLimitWithinAWindow()
    {
        var limiter = new InProcessRateLimiter(new ManualTimeProvider(Start));

        for (var i = 0; i < 3; i++)
        {
            limiter.TryAcquire("prompt", "t1", Window, 3).ShouldBeTrue();
        }

        limiter.TryAcquire("prompt", "t1", Window, 3).ShouldBeFalse();
    }

    [Fact]
    public void PoliciesAndTenantsHaveSeparateBuckets()
    {
        var limiter = new InProcessRateLimiter(new ManualTimeProvider(Start));
        limiter.TryAcquire("prompt", "t1", Window, 1).ShouldBeTrue();

        limiter.TryAcquire("prompt", "t1", Window, 1).ShouldBeFalse();
        limiter.TryAcquire("prompt", "t2", Window, 1).ShouldBeTrue();
        limiter.TryAcquire("command", "t1", Window, 1).ShouldBeTrue();
    }

    [Fact]
    public void ANewWindowStartsWithAFullBucket()
    {
        var clock = new ManualTimeProvider(Start);
        var limiter = new InProcessRateLimiter(clock);
        limiter.TryAcquire("prompt", "t1", Window, 1).ShouldBeTrue();
        limiter.TryAcquire("prompt", "t1", Window, 1).ShouldBeFalse();

        clock.Advance(Window);

        limiter.TryAcquire("prompt", "t1", Window, 1).ShouldBeTrue();
    }

    [Fact]
    public void AnExpiredBucketIsDroppedAndAFreshOneDoesNotInheritItsCount()
    {
        var clock = new ManualTimeProvider(Start);
        var limiter = new InProcessRateLimiter(clock);
        limiter.TryAcquire("prompt", "t1", Window, 1).ShouldBeTrue();

        clock.Advance(Window + InProcessRateLimiter.Grace + TimeSpan.FromSeconds(1));
        limiter.TryAcquire("other", "t9", Window, 1).ShouldBeTrue();

        limiter.TryAcquire("prompt", "t1", Window, 1).ShouldBeTrue();
    }

    [Fact]
    public void ALimitOfZeroOrLessIsRefusedSoTheLimiterIsNeverUnlimited()
    {
        var limiter = new InProcessRateLimiter(new ManualTimeProvider(Start));

        Should.Throw<ArgumentOutOfRangeException>(() => limiter.TryAcquire("prompt", "t1", Window, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => limiter.TryAcquire("prompt", "t1", TimeSpan.Zero, 1));
    }
}
