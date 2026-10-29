using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Resilience.Tests;

/// <summary>Deadline, retry, breaker, bulkhead and telemetry composed in one pipeline on a manual clock (D-40, D-41, D-42, D-63).</summary>
public sealed class ResiliencePipelineTests
{
    private const string Secret = "Host=db.internal;Password=hunter2";

    private static readonly string[] AllowedTagKeys = ["component", "operation_class", "result", "reason"];

    private static Task<int> Hang(CancellationToken cancellationToken) => HangAsync(cancellationToken);

    private static async Task<int> HangAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A driver exception that quotes the connection string, the way a provider message can.
            throw new InvalidOperationException(Secret);
        }

        return 0;
    }

    [Fact]
    public async Task ASuccessfulCallReturnsTheValueAndLeavesTheComponentHealthy()
    {
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), new ManualTimeProvider());

        var value = await pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: true, _ => Task.FromResult(42), CancellationToken.None);

        value.ShouldBe(42);
        pipeline.State.ShouldBe(DependencyState.Healthy);
        pipeline.Component.ShouldBe("postgres");
    }

    [Fact]
    public async Task ACancelledCallerTokenPassesThroughBeforeTheOperationRuns()
    {
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), new ManualTimeProvider());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var calls = 0;

        var thrown = await Should.ThrowAsync<OperationCanceledException>(
            pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: true, _ => Task.FromResult(++calls), cts.Token));

        thrown.ShouldNotBeOfType<DataPlaneTimeoutException>();
        calls.ShouldBe(0);
    }

    [Fact]
    public async Task ACallerCancellationDuringTheOperationPassesThroughUnwrappedWithoutRetryOrBreakerFailure()
    {
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), new ManualTimeProvider());
        using var cts = new CancellationTokenSource();
        var calls = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var running = pipeline.ExecuteAsync(
            OperationClass.Interactive,
            idempotent: true,
            async ct =>
            {
                calls++;
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
                return 0;
            },
            cts.Token);
        await started.Task;
        await cts.CancelAsync();

        var thrown = await Should.ThrowAsync<OperationCanceledException>(running);

        thrown.ShouldNotBeOfType<DataPlaneTimeoutException>();
        calls.ShouldBe(1, "a cancelled caller is never retried");
        pipeline.State.ShouldBe(DependencyState.Healthy, "a cancellation is not a dependency failure");
    }

    [Fact]
    public async Task ADeadlineBecomesADataPlaneTimeoutThatNamesTheComponentAndNeverTheConnectionString()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock);

        var running = pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: false, Hang, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(5));
        var thrown = await Should.ThrowAsync<DataPlaneTimeoutException>(running);

        thrown.Component.ShouldBe("postgres");
        thrown.Reason.ShouldBe(DataPlaneTimeoutReason.Timeout);
        thrown.InnerException.ShouldBeNull();
        thrown.ToString().ShouldNotContain("hunter2");
        thrown.ToString().ShouldNotContain("db.internal");
        thrown.Message.ShouldContain("postgres");
    }

    [Fact]
    public async Task InteractiveCallsTimeOutAtFiveSecondsAndBackgroundCallsAtThirty()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock);

        var background = pipeline.ExecuteAsync(OperationClass.Background, idempotent: false, Hang, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(29));
        background.IsCompleted.ShouldBeFalse("the background command timeout is 30 seconds");
        clock.Advance(TimeSpan.FromSeconds(1));
        (await Should.ThrowAsync<DataPlaneTimeoutException>(background)).Component.ShouldBe("postgres");

        var interactive = pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: false, Hang, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(4));
        interactive.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(1));
        await Should.ThrowAsync<DataPlaneTimeoutException>(interactive);
    }

    [Fact]
    public async Task DragonflyCallsTimeOutAfter250Milliseconds()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Dragonfly(), clock);

        var running = pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: false, Hang, CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(249));
        running.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(1));

        (await Should.ThrowAsync<DataPlaneTimeoutException>(running)).Component.ShouldBe("dragonfly");
    }

    [Fact]
    public async Task AnIdempotentCallRetriesATransientFailureAndSucceeds()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock, new Random(11));
        var calls = 0;

        var result = await clock.DriveAsync(pipeline.ExecuteAsync(
            OperationClass.Interactive,
            idempotent: true,
            _ => ++calls < 3 ? throw new TransientFault() : Task.FromResult("ok"),
            CancellationToken.None));

        result.ShouldBe("ok");
        calls.ShouldBe(3, "two retries after the first attempt");
    }

    [Fact]
    public async Task APostgresCallStopsAfterTwoRetries()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock, new Random(11));
        var calls = 0;

        await Should.ThrowAsync<TransientFault>(clock.DriveAsync(pipeline.ExecuteAsync<int>(
            OperationClass.Interactive,
            idempotent: true,
            _ =>
            {
                calls++;
                throw new TransientFault();
            },
            CancellationToken.None)));

        calls.ShouldBe(3);
    }

    [Fact]
    public async Task ADragonflyCallStopsAfterOneRetry()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Dragonfly(), clock, new Random(11));
        var calls = 0;

        await Should.ThrowAsync<TransientFault>(clock.DriveAsync(pipeline.ExecuteAsync<int>(
            OperationClass.Interactive,
            idempotent: true,
            _ =>
            {
                calls++;
                throw new TransientFault();
            },
            CancellationToken.None)));

        calls.ShouldBe(2);
    }

    [Fact]
    public async Task ARetryWaitsADelayInsideTheConfiguredBounds()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock, new Random(5));
        var calls = 0;

        await clock.DriveAsync(pipeline.ExecuteAsync(
            OperationClass.Interactive,
            idempotent: true,
            _ => ++calls < 2 ? throw new TransientFault() : Task.FromResult(1),
            CancellationToken.None));

        // The requested delays include the deadline timers (5 s, 8 s); the retry wait is the only one between 100 ms and 2 s.
        var waits = clock.RequestedDelays.Where(delay => delay < TimeSpan.FromSeconds(5)).ToList();
        waits.Count.ShouldBe(1);
        waits[0].ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(100));
        waits[0].ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AWriteThatIsNotMarkedIdempotentIsNeverRetried()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock);
        var calls = 0;

        await Should.ThrowAsync<TransientFault>(pipeline.ExecuteAsync<int>(
            OperationClass.Interactive,
            idempotent: false,
            _ =>
            {
                calls++;
                throw new TransientFault();
            },
            CancellationToken.None));

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task ANonTransientFailureIsNeverRetriedAndPassesThroughUnchanged()
    {
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), new ManualTimeProvider());
        var calls = 0;
        var original = new InvalidOperationException("unique violation");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(pipeline.ExecuteAsync<int>(
            OperationClass.Interactive,
            idempotent: true,
            _ =>
            {
                calls++;
                throw original;
            },
            CancellationToken.None));

        thrown.ShouldBeSameAs(original);
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task AHangingIdempotentCallIsRetriedUntilTheTotalDeadlineThenTimesOut()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock, new Random(3));
        var calls = 0;

        var thrown = await Should.ThrowAsync<DataPlaneTimeoutException>(clock.DriveAsync(pipeline.ExecuteAsync(
            OperationClass.Interactive,
            idempotent: true,
            ct =>
            {
                calls++;
                return Hang(ct);
            },
            CancellationToken.None)));

        thrown.Component.ShouldBe("postgres");
        calls.ShouldBeInRange(1, 3);
        clock.GetUtcNow().ShouldBeLessThanOrEqualTo(new DateTimeOffset(2026, 10, 27, 9, 0, 8, TimeSpan.Zero), "nothing runs past the 8 second budget");
    }

    [Fact]
    public async Task NoRetryStartsOnceTheEightSecondBudgetCannotCoverTheNextDelay()
    {
        var clock = new ManualTimeProvider();
        var policy = TestPolicies.Postgres() with { MinDelay = TimeSpan.FromSeconds(1), MaxDelay = TimeSpan.FromSeconds(1) };
        var pipeline = new ResiliencePipeline(policy, clock, new Random(1));
        var calls = 0;

        await Should.ThrowAsync<TransientFault>(clock.DriveAsync(pipeline.ExecuteAsync<int>(
            OperationClass.Interactive,
            idempotent: true,
            _ =>
            {
                calls++;

                // Attempt 1 fails at t=4 s, waits 1 s, attempt 2 fails at t=7 s: 7 s plus the 1 s delay reaches the 8 s budget.
                clock.Advance(TimeSpan.FromSeconds(calls == 1 ? 4 : 2));
                throw new TransientFault();
            },
            CancellationToken.None)));

        calls.ShouldBe(2);
    }

    [Fact]
    public async Task ASerialisationFailureRetriesTheWholeTransactionOnce()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock, new Random(2));
        var calls = 0;

        var result = await clock.DriveAsync(pipeline.ExecuteTransactionAsync(
            OperationClass.Interactive,
            _ => ++calls < 2 ? throw new SerializationFault() : Task.FromResult("committed"),
            CancellationToken.None));

        result.ShouldBe("committed");
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task ASecondSerialisationFailureSurfacesUnchanged()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock, new Random(2));
        var calls = 0;

        await Should.ThrowAsync<SerializationFault>(clock.DriveAsync(pipeline.ExecuteTransactionAsync<int>(
            OperationClass.Interactive,
            _ =>
            {
                calls++;
                throw new SerializationFault();
            },
            CancellationToken.None)));

        calls.ShouldBe(2);
    }

    [Fact]
    public async Task ATransactionDoesNotRetryAFailureThatIsNotASerialisationFailure()
    {
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), new ManualTimeProvider());
        var calls = 0;

        await Should.ThrowAsync<TransientFault>(pipeline.ExecuteTransactionAsync<int>(
            OperationClass.Interactive,
            _ =>
            {
                calls++;
                throw new TransientFault();
            },
            CancellationToken.None));

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task SerialisationFailuresAndOtherNonTransientFailuresNeverOpenTheBreaker()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock, new Random(2));

        for (var i = 0; i < 6; i++)
        {
            await Should.ThrowAsync<SerializationFault>(clock.DriveAsync(pipeline.ExecuteTransactionAsync<int>(
                OperationClass.Interactive,
                _ => throw new SerializationFault(),
                CancellationToken.None)));
            await Should.ThrowAsync<InvalidOperationException>(pipeline.ExecuteAsync<int>(
                OperationClass.Interactive,
                idempotent: false,
                _ => throw new InvalidOperationException("constraint"),
                CancellationToken.None));
        }

        pipeline.State.ShouldBe(DependencyState.Healthy);
        (await pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: false, _ => Task.FromResult(1), CancellationToken.None)).ShouldBe(1);
    }

    [Fact]
    public async Task FiveConsecutiveTransientFailuresOpenTheBreakerAndLaterCallsFailFastWithoutTouchingTheStore()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock);
        var calls = 0;

        for (var i = 0; i < 5; i++)
        {
            await Should.ThrowAsync<TransientFault>(pipeline.ExecuteAsync<int>(
                OperationClass.Interactive,
                idempotent: false,
                _ =>
                {
                    calls++;
                    throw new TransientFault();
                },
                CancellationToken.None));
        }

        pipeline.State.ShouldBe(DependencyState.Open);

        var rejected = await Should.ThrowAsync<DataPlaneTimeoutException>(pipeline.ExecuteAsync(
            OperationClass.Interactive,
            idempotent: false,
            _ => Task.FromResult(++calls),
            CancellationToken.None));

        rejected.Reason.ShouldBe(DataPlaneTimeoutReason.CircuitOpen);
        rejected.Component.ShouldBe("postgres");
        calls.ShouldBe(5, "the open breaker must not reach the store");
    }

    [Fact]
    public async Task TheFirstCallAfterTheOpenWindowIsTheProbeAndItsSuccessClosesTheBreaker()
    {
        // D-57: StartupGateHostedService probes once a second; the first probe after 15 s must be able to close the breaker.
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock);
        for (var i = 0; i < 5; i++)
        {
            await Should.ThrowAsync<TransientFault>(pipeline.ExecuteAsync<int>(OperationClass.Interactive, false, _ => throw new TransientFault(), CancellationToken.None));
        }

        clock.Advance(TimeSpan.FromSeconds(14));
        await Should.ThrowAsync<DataPlaneTimeoutException>(pipeline.ExecuteAsync(OperationClass.Interactive, false, _ => Task.FromResult(1), CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(1));

        (await pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: false, _ => Task.FromResult(9), CancellationToken.None)).ShouldBe(9);

        pipeline.State.ShouldBe(DependencyState.Healthy);
        (await pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: false, _ => Task.FromResult(10), CancellationToken.None)).ShouldBe(10);
    }

    [Fact]
    public async Task FiveTimeoutsInARowOpenTheBreaker()
    {
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock);

        for (var i = 0; i < 5; i++)
        {
            var running = pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: false, Hang, CancellationToken.None);
            clock.Advance(TimeSpan.FromSeconds(5));
            await Should.ThrowAsync<DataPlaneTimeoutException>(running);
        }

        pipeline.State.ShouldBe(DependencyState.Open);
    }

    [Fact]
    public async Task OpeningTheBreakerIsLoggedAtWarningWithoutAnySecret()
    {
        var logger = new CapturingLogger();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), new ManualTimeProvider(), logger: logger);

        for (var i = 0; i < 5; i++)
        {
            await Should.ThrowAsync<TransientFault>(pipeline.ExecuteAsync<int>(
                OperationClass.Interactive,
                idempotent: false,
                _ => throw new TransientFault(Secret),
                CancellationToken.None));
        }

        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("postgres", StringComparison.Ordinal) && entry.EventId.Id != 0);
        logger.Entries.ShouldAllBe(entry => !entry.Message.Contains("hunter2", StringComparison.Ordinal) && entry.Exception == null);
    }

    [Fact]
    public async Task TheMeterReportsTheFourInstrumentsWithOnlyTheBoundedTags()
    {
        var names = new ConcurrentDictionary<string, byte>();
        var badKeys = new ConcurrentBag<string>();

        void OnMeasurement(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            names[instrument.Name] = 0;
            foreach (var tag in tags)
            {
                if (Array.IndexOf(AllowedTagKeys, tag.Key) < 0)
                {
                    badKeys.Add($"{instrument.Name}:{tag.Key}");
                }
            }
        }

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ResilienceTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => OnMeasurement(instrument, tags));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => OnMeasurement(instrument, tags));
        listener.Start();

        // One scenario that touches every instrument: a duration, a retry, a breaker transition and a bulkhead rejection.
        var clock = new ManualTimeProvider();
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), clock, new Random(4));
        var calls = 0;
        await clock.DriveAsync(pipeline.ExecuteAsync(
            OperationClass.Interactive,
            idempotent: true,
            _ => ++calls < 2 ? throw new TransientFault() : Task.FromResult(1),
            CancellationToken.None));
        for (var i = 0; i < 5; i++)
        {
            await Should.ThrowAsync<TransientFault>(pipeline.ExecuteAsync<int>(OperationClass.Interactive, false, _ => throw new TransientFault(), CancellationToken.None));
        }

        var ledger = new ResiliencePipeline(TestPolicies.Ledger(), clock);
        var gates = new List<TaskCompletionSource<int>>();
        var running = new List<Task>();
        for (var i = 0; i < 2; i++)
        {
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            gates.Add(gate);
            running.Add(ledger.ExecuteAsync(OperationClass.Background, false, _ => gate.Task, CancellationToken.None));
        }

        await Should.ThrowAsync<DataPlaneTimeoutException>(ledger.ExecuteAsync(OperationClass.Background, false, _ => Task.FromResult(1), CancellationToken.None));
        foreach (var gate in gates)
        {
            gate.SetResult(1);
        }

        await Task.WhenAll(running);

        names.Keys.ShouldContain("autonogrammer.dataplane.call.duration");
        names.Keys.ShouldContain("autonogrammer.dataplane.retry.count");
        names.Keys.ShouldContain("autonogrammer.dataplane.breaker.transitions");
        names.Keys.ShouldContain("autonogrammer.dataplane.bulkhead.rejected");
        names.Keys.Count(name => name.StartsWith("autonogrammer.dataplane.", StringComparison.Ordinal)).ShouldBe(4);
        badKeys.ShouldBeEmpty();
        ResilienceTelemetry.MeterName.ShouldBe("Autonogrammer.Resilience");
    }

    [Fact]
    public void TheShippedDefaultsAreTheD41Table()
    {
        var postgres = new PostgresResilienceOptions();
        postgres.ConnectSeconds.ShouldBe(5);
        postgres.InteractiveCommandSeconds.ShouldBe(5);
        postgres.BackgroundCommandSeconds.ShouldBe(30);
        postgres.MaxRetries.ShouldBe(2);
        postgres.MinDelayMs.ShouldBe(100);
        postgres.MaxDelayMs.ShouldBe(2000);
        postgres.BudgetSeconds.ShouldBe(8);
        postgres.PoolInteractive.ShouldBe(16);
        postgres.PoolBackground.ShouldBe(4);
        postgres.BreakerOpenSeconds.ShouldBe(15);

        var dragonfly = new DragonflyResilienceOptions();
        dragonfly.OpMs.ShouldBe(250);
        dragonfly.ConnectSeconds.ShouldBe(2);
        dragonfly.MaxRetries.ShouldBe(1);
        dragonfly.MinDelayMs.ShouldBe(20);
        dragonfly.MaxDelayMs.ShouldBe(80);
        dragonfly.InFlight.ShouldBe(64);
        dragonfly.BreakerOpenSeconds.ShouldBe(10);

        var ledger = new LedgerResilienceOptions();
        ledger.TimeoutSeconds.ShouldBe(10);
        ledger.MaxAttempts.ShouldBe(8);
        ledger.FirstDelaySeconds.ShouldBe(5);
        ledger.MaxDelaySeconds.ShouldBe(600);
        ledger.InFlight.ShouldBe(2);
        ledger.BreakerOpenSeconds.ShouldBe(60);

        PostgresResilienceOptions.SectionName.ShouldStartWith("DataPlane:");
        DragonflyResilienceOptions.SectionName.ShouldStartWith("DataPlane:");
        LedgerResilienceOptions.SectionName.ShouldStartWith("DataPlane:");
    }
}
