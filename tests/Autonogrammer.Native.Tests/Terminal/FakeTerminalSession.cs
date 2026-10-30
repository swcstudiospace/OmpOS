using Autonogrammer.Harness.Terminal;

namespace Autonogrammer.Native.Tests.Terminal;

/// <summary>
/// An in-memory terminal session for the omp terminal model and bridge tests: it records what was written and resized,
/// counts disposals, and exits when the test says so.
/// </summary>
internal sealed class FakeTerminalSession : ITerminalSession
{
    private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposals;

    /// <summary>Gets the text written to the session, in order.</summary>
    public List<string> Writes { get; } = [];

    /// <summary>Gets each size the session was resized to, in order.</summary>
    public List<(int Columns, int Rows)> Resizes { get; } = [];

    /// <summary>Gets the number of times the session was disposed.</summary>
    public int Disposals => Volatile.Read(ref _disposals);

    /// <summary>Makes <see cref="WaitForExitAsync"/> complete with the exit code.</summary>
    /// <param name="exitCode">The exit code.</param>
    public void Exit(int exitCode) => _exit.TrySetResult(exitCode);

    /// <inheritdoc />
    public Task WriteInputAsync(string text, CancellationToken cancellationToken)
    {
        Writes.Add(text);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Resize(int columns, int rows) => Resizes.Add((columns, rows));

    /// <inheritdoc />
    public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => _exit.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposals);
        _exit.TrySetException(new ObjectDisposedException(nameof(FakeTerminalSession)));
        return ValueTask.CompletedTask;
    }
}
