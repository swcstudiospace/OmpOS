using Autonogrammer.Harness.ComputerUse.Policy;

using Shouldly;

using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>60 per minute, burst 10, step limit 300 plus 100, and 5 failures.</summary>
public sealed class RateLimiterTests
{
    [Fact]
    public void DefaultsMatchTheDocumentedNumbers()
    {
        RateLimiter limiter = new(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        limiter.PerMinute.ShouldBe(60);
        limiter.Burst.ShouldBe(10);
        limiter.StepLimit.ShouldBe(300);
        limiter.ContinueGrant.ShouldBe(100);
        limiter.FailurePause.ShouldBe(5);
    }

    [Fact]
    public void BurstIsTenAndTheMinuteCapIsSixty()
    {
        ManualTimeProvider time = new(DateTimeOffset.Parse("2026-10-30T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        RateLimiter limiter = new(time);
        for (int i = 0; i < 10; i++)
        {
            limiter.TryConsume().ShouldBe(RateLimitReason.None);
        }

        limiter.TryConsume().ShouldBe(RateLimitReason.RateLimited);
        time.Advance(TimeSpan.FromSeconds(1));
        limiter.TryConsume().ShouldBe(RateLimitReason.None);
    }

    [Fact]
    public void StepLimitIs300ThenContinueAdds100()
    {
        ManualTimeProvider time = new(DateTimeOffset.Parse("2026-10-30T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        RateLimiter limiter = new(time, perMinute: 10_000, burst: 10_000, stepLimit: 300, continueGrant: 100, failurePause: 5);
        for (int i = 0; i < 300; i++)
        {
            limiter.TryConsume().ShouldBe(RateLimitReason.None);
        }

        limiter.TryConsume().ShouldBe(RateLimitReason.StepLimit);
        limiter.GrantContinue();
        for (int i = 0; i < 100; i++)
        {
            limiter.TryConsume().ShouldBe(RateLimitReason.None);
        }

        limiter.TryConsume().ShouldBe(RateLimitReason.StepLimit);
    }

    [Fact]
    public void FiveConsecutiveFailuresPauseTheLoop()
    {
        RateLimiter limiter = new(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        for (int i = 0; i < 4; i++)
        {
            limiter.NoteFailure();
        }

        limiter.IsPaused.ShouldBeFalse();
        limiter.NoteFailure();
        limiter.IsPaused.ShouldBeTrue();
        limiter.TryConsume().ShouldBe(RateLimitReason.FailurePause);
    }
}
