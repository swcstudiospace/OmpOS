using System.Diagnostics.CodeAnalysis;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Deterministic workflow context for contract tests that records heartbeats.
/// </summary>
internal sealed class TestWorkflowContext : IWorkflowContext
{
    private readonly DateTimeOffset _fixedTime;
    private readonly Random _random;
    private readonly List<string> _heartbeats = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="TestWorkflowContext"/> class.
    /// </summary>
    /// <param name="fixedTime">The fixed UTC time (defaults to now).</param>
    /// <param name="randomSeed">The random seed (defaults to non-deterministic).</param>
    [SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification = "Test-only deterministic clock/random; not security-sensitive.")]
    public TestWorkflowContext(DateTimeOffset? fixedTime = null, int? randomSeed = null)
    {
        _fixedTime = fixedTime ?? DateTimeOffset.UtcNow;
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => _fixedTime;

    /// <inheritdoc />
    public Guid NewGuid() => Guid.NewGuid();

    /// <inheritdoc />
    public Random Random => _random;

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Heartbeat(string details)
    {
        _heartbeats.Add(details);
    }

    /// <summary>Gets the recorded heartbeat payloads.</summary>
    public IReadOnlyList<string> Heartbeats => _heartbeats;
}
