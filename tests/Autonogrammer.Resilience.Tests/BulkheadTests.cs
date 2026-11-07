using Shouldly;
using Xunit;

namespace Autonogrammer.Resilience.Tests;

/// <summary>Concurrency caps and rejection counting (D-41: 16 interactive plus 4 background, 64 Dragonfly, 2 ledger).</summary>
public sealed class BulkheadTests
{
    private static List<TaskCompletionSource<int>> Hold(ResiliencePipeline pipeline, OperationClass operationClass, int count, List<Task> running)
    {
        var gates = new List<TaskCompletionSource<int>>();
        for (var i = 0; i < count; i++)
        {
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            gates.Add(gate);
            running.Add(pipeline.ExecuteAsync(operationClass, idempotent: false, ct => gate.Task.WaitAsync(ct), CancellationToken.None));
        }

        return gates;
    }

    [Fact]
    public void ABulkheadAdmitsUpToItsCapacityThenRejectsAndCounts()
    {
        var bulkhead = new Bulkhead(2);

        bulkhead.TryEnter().ShouldBeTrue();
        bulkhead.TryEnter().ShouldBeTrue();
        bulkhead.TryEnter().ShouldBeFalse();
        bulkhead.TryEnter().ShouldBeFalse();

        bulkhead.InFlight.ShouldBe(2);
        bulkhead.Rejected.ShouldBe(2);

        bulkhead.Exit();

        bulkhead.TryEnter().ShouldBeTrue();
        bulkhead.Rejected.ShouldBe(2, "a rejection is counted once and never reset");
    }

    [Fact]
    public void ABulkheadRefusesAnInvalidCapacity()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Bulkhead(0));
    }

    [Fact]
    public void ThePostgresCapsAre16InteractiveAnd4BackgroundInsideAPoolOf20()
    {
        var policy = TestPolicies.Postgres();

        policy.InteractiveInFlight.ShouldBe(16);
        policy.BackgroundInFlight.ShouldBe(4);
        (policy.InteractiveInFlight + policy.BackgroundInFlight!.Value).ShouldBe(20);
        TestPolicies.Dragonfly().InteractiveInFlight.ShouldBe(64);
        TestPolicies.Dragonfly().BackgroundInFlight.ShouldBeNull();
        TestPolicies.Ledger().InteractiveInFlight.ShouldBe(2);
        TestPolicies.Ledger().BackgroundInFlight.ShouldBeNull();
    }

    [Fact]
    public async Task PostgresRejectsTheFifthBackgroundCallButStillServesInteractiveCalls()
    {
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), new ManualTimeProvider());
        var running = new List<Task>();
        var gates = Hold(pipeline, OperationClass.Background, 4, running);

        var rejected = await Should.ThrowAsync<DataPlaneTimeoutException>(
            pipeline.ExecuteAsync(OperationClass.Background, idempotent: false, _ => Task.FromResult(1), CancellationToken.None)).ConfigureAwait(true);
        rejected.Reason.ShouldBe(DataPlaneTimeoutReason.BulkheadFull);
        rejected.Component.ShouldBe("postgres");

        (await pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: false, _ => Task.FromResult(7), CancellationToken.None).ConfigureAwait(true)).ShouldBe(7);

        foreach (var gate in gates)
        {
            gate.SetResult(1);
        }

        await Task.WhenAll(running).ConfigureAwait(true);
        pipeline.State.ShouldBe(DependencyState.Healthy, "a full bulkhead is local overload, not a dependency failure");
        (await pipeline.ExecuteAsync(OperationClass.Background, idempotent: false, _ => Task.FromResult(2), CancellationToken.None).ConfigureAwait(true)).ShouldBe(2);
    }

    [Fact]
    public async Task PostgresRejectsTheSeventeenthInteractiveCall()
    {
        var pipeline = new ResiliencePipeline(TestPolicies.Postgres(), new ManualTimeProvider());
        var running = new List<Task>();
        var gates = Hold(pipeline, OperationClass.Interactive, 16, running);

        var rejected = await Should.ThrowAsync<DataPlaneTimeoutException>(
            pipeline.ExecuteAsync(OperationClass.Interactive, idempotent: false, _ => Task.FromResult(1), CancellationToken.None)).ConfigureAwait(true);
        rejected.Reason.ShouldBe(DataPlaneTimeoutReason.BulkheadFull);

        foreach (var gate in gates)
        {
            gate.SetResult(1);
        }

        await Task.WhenAll(running).ConfigureAwait(true);
    }

    [Fact]
    public async Task DragonflyAdmits64CallsInFlightAndRejectsTheNext()
    {
        var pipeline = new ResiliencePipeline(TestPolicies.Dragonfly(), new ManualTimeProvider());
        var running = new List<Task>();
        var gates = Hold(pipeline, OperationClass.Interactive, 64, running);

        var rejected = await Should.ThrowAsync<DataPlaneTimeoutException>(
            pipeline.ExecuteAsync(OperationClass.Background, idempotent: false, _ => Task.FromResult(1), CancellationToken.None)).ConfigureAwait(true);
        rejected.Reason.ShouldBe(DataPlaneTimeoutReason.BulkheadFull);
        rejected.Component.ShouldBe("dragonfly");

        foreach (var gate in gates)
        {
            gate.SetResult(1);
        }

        await Task.WhenAll(running).ConfigureAwait(true);
    }

    [Fact]
    public async Task TheLedgerAdmitsTwoCallsInFlightAndRejectsTheThird()
    {
        var pipeline = new ResiliencePipeline(TestPolicies.Ledger(), new ManualTimeProvider());
        var running = new List<Task>();
        var gates = Hold(pipeline, OperationClass.Background, 2, running);

        var rejected = await Should.ThrowAsync<DataPlaneTimeoutException>(
            pipeline.ExecuteAsync(OperationClass.Background, idempotent: false, _ => Task.FromResult(1), CancellationToken.None)).ConfigureAwait(true);
        rejected.Reason.ShouldBe(DataPlaneTimeoutReason.BulkheadFull);
        rejected.Component.ShouldBe("ledger");

        foreach (var gate in gates)
        {
            gate.SetResult(1);
        }

        await Task.WhenAll(running).ConfigureAwait(true);
    }

    [Fact]
    public async Task ASlotIsReleasedWhenTheOperationThrows()
    {
        var pipeline = new ResiliencePipeline(TestPolicies.Ledger(), new ManualTimeProvider());

        for (var i = 0; i < 5; i++)
        {
            await Should.ThrowAsync<InvalidOperationException>(
                pipeline.ExecuteAsync<int>(OperationClass.Background, idempotent: false, _ => throw new InvalidOperationException("boom"), CancellationToken.None)).ConfigureAwait(true);
        }

        (await pipeline.ExecuteAsync(OperationClass.Background, idempotent: false, _ => Task.FromResult(3), CancellationToken.None).ConfigureAwait(true)).ShouldBe(3);
    }
}
