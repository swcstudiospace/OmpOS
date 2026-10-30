using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Terminal;
using Autonogrammer.Native.Views.Terminal;

namespace Autonogrammer.Native.Tests.Terminal;

/// <summary>
/// An <see cref="IOmpTerminalStarter"/> for tests. Each start returns the next queued launch result, or a fake session when
/// the queue is empty, and records the size and output sink it was given. A start can wait on a gate so a test can stop the
/// model while omp is still launching.
/// </summary>
internal sealed class FakeOmpTerminalStarter : IOmpTerminalStarter
{
    private readonly Queue<OmpTerminalLaunchResult> _results = new();

    /// <summary>Gets the sizes each start was asked for, in order.</summary>
    public List<(int Columns, int Rows)> Sizes { get; } = [];

    /// <summary>Gets the output sink the latest start was given.</summary>
    public IProcessOutputSink? Output { get; private set; }

    /// <summary>Gets the sessions this starter created for starts with an empty queue, in order.</summary>
    public List<FakeTerminalSession> Sessions { get; } = [];

    /// <summary>Gets or sets a task every start waits for before it returns, or null to return at once.</summary>
    public Task? Gate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the wait on <see cref="Gate"/> ends with a cancellation when the model cancels the
    /// launch. False models a starter that finishes the spawn anyway, so the model receives a session it no longer wants.
    /// </summary>
    public bool HonoursCancellation { get; set; }

    /// <summary>Queues the result of the next start.</summary>
    /// <param name="result">The result.</param>
    public void Enqueue(OmpTerminalLaunchResult result) => _results.Enqueue(result);

    /// <inheritdoc />
    public async Task<OmpTerminalLaunchResult> StartAsync(
        int columns,
        int rows,
        IProcessOutputSink output,
        CancellationToken cancellationToken)
    {
        Sizes.Add((columns, rows));
        Output = output;
        if (Gate is not null)
        {
            await (HonoursCancellation ? Gate.WaitAsync(cancellationToken) : Gate).ConfigureAwait(true);
        }

        if (_results.Count > 0)
        {
            return _results.Dequeue();
        }

        var session = new FakeTerminalSession();
        Sessions.Add(session);
        return new OmpTerminalStarted("/usr/bin/omp", "1.2.3", session);
    }
}
