using Autonogrammer.EngineHost;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The retry schedule of a remote connection.</summary>
public sealed class ReconnectPolicyTests
{
    /// <summary>The default schedule waits 1, 2, 4, 8 and 16 seconds.</summary>
    [Fact]
    public void TheDefaultScheduleDoubles()
    {
        var policy = ReconnectPolicy.Default;

        policy.MaxAttempts.ShouldBe(5);
        Enumerable.Range(1, 5).Select(policy.DelayBefore).ShouldBe(
        [
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(16),
        ]);
    }

    /// <summary>A delay never exceeds the maximum.</summary>
    [Fact]
    public void TheDelayIsCapped()
    {
        var policy = new ReconnectPolicy { InitialDelay = TimeSpan.FromSeconds(10), MaxDelay = TimeSpan.FromSeconds(25), MaxAttempts = 5 };

        Enumerable.Range(1, 5).Select(policy.DelayBefore).ShouldBe(
        [
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(25),
            TimeSpan.FromSeconds(25),
            TimeSpan.FromSeconds(25),
        ]);
    }

    /// <summary>An attempt number outside the schedule is refused.</summary>
    [Fact]
    public void AnAttemptOutsideTheScheduleIsRefused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ReconnectPolicy.Default.DelayBefore(0));
        Should.Throw<ArgumentOutOfRangeException>(() => ReconnectPolicy.Default.DelayBefore(6));
    }

    /// <summary>A policy with a non-positive delay, a maximum below the initial delay or a bad attempt count is invalid.</summary>
    [Fact]
    public void InvalidPoliciesAreRefused()
    {
        ReconnectPolicy.Default.Validate();
        Should.Throw<ArgumentException>(() => (ReconnectPolicy.Default with { InitialDelay = TimeSpan.Zero }).Validate());
        Should.Throw<ArgumentException>(() => (ReconnectPolicy.Default with { MaxDelay = TimeSpan.FromMilliseconds(500) }).Validate());
        Should.Throw<ArgumentException>(() => (ReconnectPolicy.Default with { MaxAttempts = 0 }).Validate());
        Should.Throw<ArgumentException>(() => (ReconnectPolicy.Default with { MaxAttempts = 101 }).Validate());
    }
}
