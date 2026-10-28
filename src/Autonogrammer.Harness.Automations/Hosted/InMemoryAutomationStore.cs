namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// An <see cref="IAutomationStore"/> that keeps everything in memory. Tests and hosts that do not need to survive a restart use it.
/// </summary>
public sealed class InMemoryAutomationStore : IAutomationStore
{
    private readonly Lock _gate = new();
    private readonly AutomationStoreDocument _document = AutomationStoreDocument.Empty();

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<UserAutomation>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_document.List());
        }
    }

    /// <inheritdoc />
    public ValueTask<UserAutomation?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            return ValueTask.FromResult(_document.Get(id));
        }
    }

    /// <inheritdoc />
    public ValueTask SaveAsync(UserAutomation automation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(automation);
        lock (_gate)
        {
            _document.Save(automation);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            return ValueTask.FromResult(_document.Remove(id));
        }
    }

    /// <inheritdoc />
    public ValueTask<DateTimeOffset?> GetScheduleCursorAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            return ValueTask.FromResult(_document.GetCursor(id));
        }
    }

    /// <inheritdoc />
    public ValueTask SetScheduleCursorAsync(string id, DateTimeOffset cursor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            _ = _document.SetCursor(id, cursor);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<AutomationRunEntry>> GetHistoryAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            return ValueTask.FromResult(_document.GetHistory(id));
        }
    }

    /// <inheritdoc />
    public ValueTask UpsertRunAsync(string id, AutomationRunEntry entry, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            _ = _document.UpsertRun(id, entry);
        }

        return ValueTask.CompletedTask;
    }
}
