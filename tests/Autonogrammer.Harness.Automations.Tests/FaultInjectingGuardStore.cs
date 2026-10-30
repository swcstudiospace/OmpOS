namespace Autonogrammer.Harness.Automations.Tests;

/// <summary>
/// A guard store that wraps a real store and throws on the operations a test selects, to prove a failing store refuses runs.
/// </summary>
internal sealed class FaultInjectingGuardStore(IAutomationGuardStore inner) : IAutomationGuardStore
{
    /// <summary>
    /// The operations that throw. Remove an entry to let that operation succeed again.
    /// </summary>
    public HashSet<GuardOperation> Faults { get; } = new();

    /// <inheritdoc />
    public ValueTask<AutomationPause?> FindPauseAsync(string automationId, CancellationToken cancellationToken)
    {
        Check(GuardOperation.FindPause);
        return inner.FindPauseAsync(automationId, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask PauseAsync(string automationId, AutomationPause pause, CancellationToken cancellationToken) =>
        inner.PauseAsync(automationId, pause, cancellationToken);

    /// <inheritdoc />
    public ValueTask ResumeAsync(string automationId, CancellationToken cancellationToken) =>
        inner.ResumeAsync(automationId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<int> CountRunsSinceAsync(string automationId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        Check(GuardOperation.CountRuns);
        return inner.CountRunsSinceAsync(automationId, since, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordRunAsync(string automationId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        Check(GuardOperation.RecordRun);
        return inner.RecordRunAsync(automationId, startedAt, cancellationToken);
    }

    private void Check(GuardOperation operation)
    {
        if (Faults.Contains(operation))
        {
            throw new InvalidOperationException($"injected {operation} fault");
        }
    }
}
