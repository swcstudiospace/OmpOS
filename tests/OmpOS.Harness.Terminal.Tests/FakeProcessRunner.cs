namespace OmpOS.Harness.Terminal.Tests;

/// <summary>
/// A runner that spawns nothing. It records every request, so a test can count spawns and check their arguments.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly List<ProcessRequest> _requests = [];

    /// <summary>
    /// The outcome returned for each run.
    /// </summary>
    public ProcessOutcome Outcome { get; set; } = new(0, string.Empty, string.Empty);

    /// <summary>
    /// When set, each run fails with this exception instead of returning <see cref="Outcome"/>.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// Every request the gate sent, in order.
    /// </summary>
    public IReadOnlyList<ProcessRequest> Requests => _requests;

    /// <summary>
    /// The number of times the gate asked for a spawn.
    /// </summary>
    public int SpawnCount => _requests.Count;

    /// <inheritdoc />
    public Task<ProcessOutcome> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return Failure is null
            ? Task.FromResult(Outcome)
            : Task.FromException<ProcessOutcome>(Failure);
    }
}
