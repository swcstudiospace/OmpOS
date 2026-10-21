namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Fake process runner for testing.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly List<ProcessSpec> _specs = [];
    private readonly Queue<ProcessResult> _queuedResults = new();

    private ProcessResult _result = new()
    {
        ExitCode = 0,
        Stdout = string.Empty,
        Stderr = string.Empty,
        TimedOut = false,
    };

    /// <summary>
    /// Gets the last process specification.
    /// </summary>
    public ProcessSpec? LastSpec { get; private set; }

    /// <summary>
    /// Gets all process specifications in call order.
    /// </summary>
    public IReadOnlyList<ProcessSpec> Specs => _specs;

    /// <summary>
    /// Gets or sets an optional fault injector. When it returns a non-null exception for a spec, RunAsync throws it.
    /// </summary>
    public Func<ProcessSpec, Exception?>? ThrowForSpec { get; set; }

    /// <summary>
    /// Gets or sets an optional gate. When it returns a task for a spec, RunAsync waits for that task before completing.
    /// </summary>
    public Func<ProcessSpec, Task?>? PauseFor { get; set; }

    /// <summary>
    /// Gets or sets the lines a streamed spec yields. Required before StreamStdoutLinesAsync is called.
    /// </summary>
    public Func<ProcessSpec, IAsyncEnumerable<string>>? StreamFor { get; set; }

    /// <summary>
    /// Sets the result to return.
    /// </summary>
    public void SetResult(ProcessResult result) => _result = result;

    /// <summary>
    /// Enqueues per-call results, consumed in order before the sticky result.
    /// </summary>
    /// <param name="results">The results to enqueue.</param>
    public void EnqueueResults(params ProcessResult[] results)
    {
        foreach (var result in results)
        {
            _queuedResults.Enqueue(result);
        }
    }

    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        LastSpec = spec;
        _specs.Add(spec);
        if (PauseFor?.Invoke(spec) is { } pause)
        {
            await pause.ConfigureAwait(false);
        }

        if (ThrowForSpec?.Invoke(spec) is Exception failure)
        {
            throw failure;
        }

        return _queuedResults.TryDequeue(out var queued) ? queued : _result;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamStdoutLinesAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        LastSpec = spec;
        _specs.Add(spec);
        var stream = StreamFor ?? throw new InvalidOperationException("No stream is configured for this fake runner.");
        return stream(spec);
    }
}
