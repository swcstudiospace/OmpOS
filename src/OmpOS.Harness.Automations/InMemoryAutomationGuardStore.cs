namespace OmpOS.Harness.Automations;

/// <summary>
/// A guard store that keeps pauses and run history in memory. State lasts as long as the instance.
/// </summary>
public sealed class InMemoryAutomationGuardStore : IAutomationGuardStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AutomationPause> _pauses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DateTimeOffset>> _runs = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<AutomationPause?> FindPauseAsync(string automationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return ValueTask.FromResult(_pauses.GetValueOrDefault(automationId));
        }
    }

    /// <inheritdoc />
    public ValueTask PauseAsync(string automationId, AutomationPause pause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pause);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _pauses[automationId] = pause;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ResumeAsync(string automationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _pauses.Remove(automationId);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<int> CountRunsSinceAsync(string automationId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var count = _runs.TryGetValue(automationId, out var starts) ? starts.Count(start => start > since) : 0;
            return ValueTask.FromResult(count);
        }
    }

    /// <inheritdoc />
    public ValueTask RecordRunAsync(string automationId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_runs.TryGetValue(automationId, out var starts))
            {
                starts = [];
                _runs[automationId] = starts;
            }

            starts.Add(startedAt);
        }

        return ValueTask.CompletedTask;
    }
}
