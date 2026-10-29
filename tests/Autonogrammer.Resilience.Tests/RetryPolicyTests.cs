using Shouldly;
using Xunit;

namespace Autonogrammer.Resilience.Tests;

/// <summary>Retry decisions and decorrelated-jitter delays (D-41, ADR-0031 D7).</summary>
public sealed class RetryPolicyTests
{
    private const int Samples = 2000;

    [Fact]
    public void PostgresDelaysStayBetween100MillisecondsAndTwoSeconds()
    {
        var retry = new RetryPolicy(TestPolicies.Postgres(), new Random(20261029));

        var previous = TimeSpan.FromMilliseconds(100);
        var longest = TimeSpan.Zero;
        for (var i = 0; i < Samples; i++)
        {
            var next = retry.NextDelay(previous);
            next.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(100));
            next.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(2));
            longest = next > longest ? next : longest;
            previous = next;
        }

        longest.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(500), "the delay has to grow, not stay at the floor");
    }

    [Fact]
    public void DragonflyDelaysStayBetween20And80Milliseconds()
    {
        var retry = new RetryPolicy(TestPolicies.Dragonfly(), new Random(20261029));

        var previous = TimeSpan.FromMilliseconds(20);
        for (var i = 0; i < Samples; i++)
        {
            var next = retry.NextDelay(previous);
            next.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(20));
            next.ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(80));
            previous = next;
        }
    }

    [Fact]
    public void AHugePreviousDelayNeverExceedsTheCap()
    {
        var retry = new RetryPolicy(TestPolicies.Postgres(), new Random(1));

        for (var i = 0; i < Samples; i++)
        {
            retry.NextDelay(TimeSpan.FromHours(1)).ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public void ASeededRandomGivesAReproducibleSchedule()
    {
        static List<TimeSpan> Schedule(int seed)
        {
            var retry = new RetryPolicy(TestPolicies.Postgres(), new Random(seed));
            var delays = new List<TimeSpan>();
            var previous = TimeSpan.FromMilliseconds(100);
            for (var i = 0; i < 10; i++)
            {
                previous = retry.NextDelay(previous);
                delays.Add(previous);
            }

            return delays;
        }

        Schedule(7).ShouldBe(Schedule(7));
        Schedule(7).ShouldNotBe(Schedule(8));
    }

    [Fact]
    public void PostgresRetriesATransientIdempotentCallTwiceAndNoMore()
    {
        var retry = new RetryPolicy(TestPolicies.Postgres(), new Random(1));
        var fault = new TransientFault();

        retry.ShouldRetry(fault, retriesDone: 0, idempotent: true).ShouldBeTrue();
        retry.ShouldRetry(fault, retriesDone: 1, idempotent: true).ShouldBeTrue();
        retry.ShouldRetry(fault, retriesDone: 2, idempotent: true).ShouldBeFalse();
    }

    [Fact]
    public void DragonflyRetriesATransientIdempotentCallOnceAndNoMore()
    {
        var retry = new RetryPolicy(TestPolicies.Dragonfly(), new Random(1));
        var fault = new TransientFault();

        retry.ShouldRetry(fault, retriesDone: 0, idempotent: true).ShouldBeTrue();
        retry.ShouldRetry(fault, retriesDone: 1, idempotent: true).ShouldBeFalse();
    }

    [Fact]
    public void ALedgerCallIsNeverRetriedByThePipeline()
    {
        var retry = new RetryPolicy(TestPolicies.Ledger(), new Random(1));

        retry.ShouldRetry(new TransientFault(), retriesDone: 0, idempotent: true).ShouldBeFalse();
    }

    [Fact]
    public void AWriteIsNotRetriedUnlessTheCallerMarksItIdempotent()
    {
        var retry = new RetryPolicy(TestPolicies.Postgres(), new Random(1));

        retry.ShouldRetry(new TransientFault(), retriesDone: 0, idempotent: false).ShouldBeFalse();
        retry.ShouldRetry(new TransientFault(), retriesDone: 0, idempotent: true).ShouldBeTrue();
    }

    [Fact]
    public void ANonTransientFailureIsNeverRetried()
    {
        var retry = new RetryPolicy(TestPolicies.Postgres(), new Random(1));

        retry.ShouldRetry(new InvalidOperationException("constraint"), retriesDone: 0, idempotent: true).ShouldBeFalse();
        retry.ShouldRetry(new SerializationFault(), retriesDone: 0, idempotent: true).ShouldBeFalse();
    }

    [Fact]
    public void ASerialisationFailureRetriesTheWholeTransactionExactlyOnce()
    {
        var retry = new RetryPolicy(TestPolicies.Postgres(), new Random(1));

        retry.ShouldRetryTransaction(new SerializationFault(), retriesDone: 0).ShouldBeTrue();
        retry.ShouldRetryTransaction(new SerializationFault(), retriesDone: 1).ShouldBeFalse();
        retry.ShouldRetryTransaction(new TransientFault(), retriesDone: 0).ShouldBeFalse();
    }

    [Fact]
    public void ThePostgresRetryBudgetIsEightSecondsAndDoesNotExceedTwiceTheCommandTimeout()
    {
        var policy = TestPolicies.Postgres();

        policy.Budget.ShouldBe(TimeSpan.FromSeconds(8));
        policy.Budget!.Value.ShouldBeLessThanOrEqualTo(policy.InteractiveTimeout * 2);
    }
}
