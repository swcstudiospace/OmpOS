using System.Threading.Channels;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Substrate;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Substrate;

/// <summary>
/// Runs a <see cref="SubstrateStageObserver"/> over a <see cref="FakeSubstrateServer"/> and a <see cref="SubstrateTestClock"/>. A
/// cycle ends when the observer waits for its next poll, so a test reads exactly the events of that cycle and then advances time.
/// </summary>
internal sealed class ObserverRig : IAsyncDisposable
{
    private static readonly TimeSpan DeadMan = TimeSpan.FromSeconds(30);

    private readonly Channel<StageEvent> _events = Channel.CreateUnbounded<StageEvent>();
    private readonly CancellationTokenSource _cancellation;
    private readonly Task _pump;

    /// <summary>Starts observing a session.</summary>
    /// <param name="scope">The session to observe.</param>
    /// <param name="server">The fake server. A default one is created and owned when null.</param>
    /// <param name="repositoryResolver">Names the repository of the workspace.</param>
    public ObserverRig(StageObservationScope scope, FakeSubstrateServer? server = null, Func<string, string?>? repositoryResolver = null)
    {
        Server = server ?? new FakeSubstrateServer();
        Clock = new SubstrateTestClock(new DateTimeOffset(2026, 10, 26, 8, 30, 0, TimeSpan.Zero));
        var observer = new SubstrateStageObserver(Server.Gateway, Clock, repositoryResolver ?? (_ => SubstrateTestSupport.Repository));
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var token = _cancellation.Token;
        _pump = Task.Run(
            async () =>
            {
                try
                {
                    await foreach (var stage in observer.ObserveAsync(scope, token).ConfigureAwait(true))
                    {
                        _events.Writer.TryWrite(stage);
                    }
                }
                finally
                {
                    _events.Writer.TryComplete();
                }
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Gets the fake server behind the observer.</summary>
    public FakeSubstrateServer Server { get; }

    /// <summary>Gets the clock that drives the observer's polling.</summary>
    public SubstrateTestClock Clock { get; }

    /// <summary>Gets the task that completes when the observation sequence ends, or faults when that takes longer than the dead-man timeout.</summary>
    public Task Completion => _pump.WaitAsync(DeadMan);

    /// <summary>Waits until the observer is waiting for its next poll and returns the events of the cycle that just ended.</summary>
    /// <returns>The events, in the order the observer produced them.</returns>
    public async Task<List<StageEvent>> NextCycleAsync()
    {
        await Clock.WaitForTimerAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var batch = new List<StageEvent>();
        while (_events.Reader.TryRead(out var stage))
        {
            batch.Add(stage);
        }

        return batch;
    }

    /// <summary>Advances the clock by exactly the pending delay, which starts the next poll.</summary>
    /// <returns>The delay that was waited.</returns>
    public TimeSpan Tick()
    {
        var delay = Clock.NextDueIn ?? throw new InvalidOperationException("The observer is not waiting for a poll.");
        Clock.Advance(delay);
        return delay;
    }

    /// <summary>Ends the observation and waits for the sequence to finish.</summary>
    /// <returns>A task that completes when the sequence has ended.</returns>
    public async Task StopAsync()
    {
        await _cancellation.CancelAsync().ConfigureAwait(true);
        await _pump.WaitAsync(DeadMan).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync().ConfigureAwait(true);
        try
        {
            await _pump.WaitAsync(DeadMan).ConfigureAwait(true);
        }
        finally
        {
            _cancellation.Dispose();
            Server.Dispose();
        }
    }
}
