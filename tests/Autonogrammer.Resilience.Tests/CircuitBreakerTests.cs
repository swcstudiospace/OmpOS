using Shouldly;
using Xunit;

namespace Autonogrammer.Resilience.Tests;

/// <summary>Breaker thresholds, open windows and the single half-open probe (D-41, D-57).</summary>
public sealed class CircuitBreakerTests
{
    private static (CircuitBreaker Breaker, ManualTimeProvider Clock) Create(ResiliencePolicy policy)
    {
        var clock = new ManualTimeProvider();
        return (new CircuitBreaker(policy, clock), clock);
    }

    private static void Fail(CircuitBreaker breaker, int times)
    {
        for (var i = 0; i < times; i++)
        {
            var permit = breaker.TryAcquire();
            permit.ShouldNotBe(BreakerPermit.Rejected);
            breaker.RecordFailure(permit);
        }
    }

    private static void Succeed(CircuitBreaker breaker)
    {
        var permit = breaker.TryAcquire();
        permit.ShouldNotBe(BreakerPermit.Rejected);
        breaker.RecordSuccess(permit);
    }

    [Fact]
    public void FourConsecutiveFailuresKeepTheBreakerClosed()
    {
        var (breaker, _) = Create(TestPolicies.Postgres());

        Fail(breaker, 4);

        breaker.TryAcquire().ShouldBe(BreakerPermit.Allowed);
        breaker.State.ShouldBe(DependencyState.Degraded);
    }

    [Fact]
    public void FiveConsecutiveFailuresOpenTheBreaker()
    {
        var (breaker, _) = Create(TestPolicies.Postgres());

        Fail(breaker, 5);

        breaker.State.ShouldBe(DependencyState.Open);
        breaker.TryAcquire().ShouldBe(BreakerPermit.Rejected);
    }

    [Fact]
    public void ASuccessResetsTheConsecutiveCount()
    {
        var (breaker, _) = Create(TestPolicies.Postgres());

        Fail(breaker, 4);
        Succeed(breaker);
        Fail(breaker, 4);

        breaker.State.ShouldNotBe(DependencyState.Open);
    }

    [Fact]
    public void FiftyPercentOfTheLastTwentyCallsOpenTheBreaker()
    {
        var (breaker, _) = Create(TestPolicies.Postgres());

        // Success, failure, success, failure...: never more than one failure in a row, so only the ratio rule can open it.
        for (var call = 1; call <= 19; call++)
        {
            if (call % 2 == 0)
            {
                Fail(breaker, 1);
            }
            else
            {
                Succeed(breaker);
            }
        }

        breaker.State.ShouldNotBe(DependencyState.Open, "9 failures in 19 calls: the window is not full and the ratio is below 50 percent");

        Fail(breaker, 1);

        breaker.State.ShouldBe(DependencyState.Open, "10 failures in the last 20 calls is 50 percent");
    }

    [Fact]
    public void FortyPercentOfTheLastTwentyCallsKeepTheBreakerClosed()
    {
        var (breaker, _) = Create(TestPolicies.Postgres());

        for (var call = 1; call <= 20; call++)
        {
            if (call % 5 == 1 || call % 5 == 3)
            {
                Fail(breaker, 1);
            }
            else
            {
                Succeed(breaker);
            }
        }

        breaker.State.ShouldNotBe(DependencyState.Open);
    }

    [Fact]
    public void CallsOlderThanThirtySecondsLeaveTheRatioWindow()
    {
        var (breaker, clock) = Create(TestPolicies.Postgres());

        for (var call = 1; call <= 10; call++)
        {
            if (call % 2 == 1)
            {
                Fail(breaker, 1);
            }
            else
            {
                Succeed(breaker);
            }
        }

        clock.Advance(TimeSpan.FromSeconds(31));

        for (var call = 1; call <= 10; call++)
        {
            if (call % 2 == 1)
            {
                Fail(breaker, 1);
            }
            else
            {
                Succeed(breaker);
            }
        }

        breaker.State.ShouldNotBe(DependencyState.Open, "the first ten calls expired, so the window holds only 10 samples");
    }

    [Fact]
    public void ANewBreakerIsHealthyAndAFailureDegradesIt()
    {
        var (breaker, _) = Create(TestPolicies.Postgres());
        breaker.State.ShouldBe(DependencyState.Healthy);

        Fail(breaker, 1);

        breaker.State.ShouldBe(DependencyState.Degraded);
    }

    [Theory]
    [InlineData("postgres", 15)]
    [InlineData("dragonfly", 10)]
    [InlineData("ledger", 60)]
    public void TheBreakerStaysOpenForTheComponentWindowThenAdmitsOneProbe(string component, int openSeconds)
    {
        var policy = component switch
        {
            "postgres" => TestPolicies.Postgres(),
            "dragonfly" => TestPolicies.Dragonfly(),
            _ => TestPolicies.Ledger(),
        };
        var (breaker, clock) = Create(policy);
        Fail(breaker, 5);

        clock.Advance(TimeSpan.FromSeconds(openSeconds) - TimeSpan.FromMilliseconds(1));
        breaker.TryAcquire().ShouldBe(BreakerPermit.Rejected);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        breaker.TryAcquire().ShouldBe(BreakerPermit.Probe);
    }

    [Fact]
    public void ExactlyOneHalfOpenProbeIsAdmittedAtATime()
    {
        var (breaker, clock) = Create(TestPolicies.Postgres());
        Fail(breaker, 5);
        clock.Advance(TimeSpan.FromSeconds(15));

        breaker.TryAcquire().ShouldBe(BreakerPermit.Probe);
        breaker.TryAcquire().ShouldBe(BreakerPermit.Rejected);
        breaker.TryAcquire().ShouldBe(BreakerPermit.Rejected);
    }

    [Fact]
    public void AFailedProbeReopensTheBreakerForAFullWindow()
    {
        var (breaker, clock) = Create(TestPolicies.Postgres());
        Fail(breaker, 5);
        clock.Advance(TimeSpan.FromSeconds(15));
        var probe = breaker.TryAcquire();

        breaker.RecordFailure(probe);

        breaker.State.ShouldBe(DependencyState.Open);
        breaker.TryAcquire().ShouldBe(BreakerPermit.Rejected);
        clock.Advance(TimeSpan.FromSeconds(14));
        breaker.TryAcquire().ShouldBe(BreakerPermit.Rejected);
        clock.Advance(TimeSpan.FromSeconds(1));
        breaker.TryAcquire().ShouldBe(BreakerPermit.Probe);
    }

    [Fact]
    public void TheFirstProbeAfterTheWindowCanCloseTheBreaker()
    {
        // D-57: the startup gate's probe is the first call after the open window; its success must close the breaker.
        var (breaker, clock) = Create(TestPolicies.Postgres());
        Fail(breaker, 5);
        clock.Advance(TimeSpan.FromSeconds(15));
        var probe = breaker.TryAcquire();

        breaker.RecordSuccess(probe);

        breaker.State.ShouldBe(DependencyState.Healthy);
        breaker.TryAcquire().ShouldBe(BreakerPermit.Allowed);
        breaker.TryAcquire().ShouldBe(BreakerPermit.Allowed);
    }

    [Fact]
    public void AProbeThatIsReleasedWithoutAVerdictFreesTheSlot()
    {
        var (breaker, clock) = Create(TestPolicies.Postgres());
        Fail(breaker, 5);
        clock.Advance(TimeSpan.FromSeconds(15));
        var probe = breaker.TryAcquire();

        breaker.Release(probe);

        breaker.TryAcquire().ShouldBe(BreakerPermit.Probe);
    }

    [Fact]
    public void ALateResultFromACallAdmittedBeforeTheBreakerOpenedChangesNothing()
    {
        var (breaker, clock) = Create(TestPolicies.Postgres());
        var late = breaker.TryAcquire();
        Fail(breaker, 5);
        clock.Advance(TimeSpan.FromSeconds(15));
        var probe = breaker.TryAcquire();

        breaker.RecordSuccess(late);

        breaker.State.ShouldBe(DependencyState.Degraded, "half-open: only the probe decides");
        breaker.TryAcquire().ShouldBe(BreakerPermit.Rejected);
        breaker.RecordSuccess(probe);
        breaker.State.ShouldBe(DependencyState.Healthy);
    }

    [Fact]
    public void ClosingTheBreakerForgetsTheOldFailures()
    {
        var (breaker, clock) = Create(TestPolicies.Postgres());
        Fail(breaker, 5);
        clock.Advance(TimeSpan.FromSeconds(15));
        breaker.RecordSuccess(breaker.TryAcquire());

        Fail(breaker, 4);

        breaker.State.ShouldBe(DependencyState.Degraded);
    }

    [Fact]
    public void TheThresholdsAreTheD41Constants()
    {
        var policy = TestPolicies.Postgres();

        policy.BreakerConsecutiveFailures.ShouldBe(5);
        policy.BreakerFailureRatio.ShouldBe(0.5);
        policy.BreakerSampledCalls.ShouldBe(20);
        policy.BreakerWindow.ShouldBe(TimeSpan.FromSeconds(30));
        policy.BreakerOpenDuration.ShouldBe(TimeSpan.FromSeconds(15));
        TestPolicies.Dragonfly().BreakerOpenDuration.ShouldBe(TimeSpan.FromSeconds(10));
        TestPolicies.Ledger().BreakerOpenDuration.ShouldBe(TimeSpan.FromSeconds(60));
    }
}
