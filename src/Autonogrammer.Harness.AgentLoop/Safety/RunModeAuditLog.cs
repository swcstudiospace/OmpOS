namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>An in-memory <see cref="IRunModeAuditSink"/> that keeps the most recent decisions, newest last.</summary>
public sealed class RunModeAuditLog : IRunModeAuditSink
{
    private readonly Lock _gate = new();
    private readonly Queue<RunModeAuditEntry> _entries = new();
    private readonly int _capacity;

    /// <summary>Initializes a new instance of the <see cref="RunModeAuditLog"/> class.</summary>
    /// <param name="capacity">How many decisions to keep.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public RunModeAuditLog(int capacity = 200)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    /// <inheritdoc/>
    public void Record(RunModeAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > _capacity)
            {
                _entries.Dequeue();
            }
        }
    }

    /// <summary>Returns the kept decisions, oldest first.</summary>
    /// <returns>A copy of the log.</returns>
    public IReadOnlyList<RunModeAuditEntry> Snapshot()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }
}
