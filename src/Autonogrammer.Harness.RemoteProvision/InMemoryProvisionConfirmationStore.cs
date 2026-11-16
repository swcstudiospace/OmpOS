namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// Keeps confirmations in memory for the life of the process. A confirmation is lost when the process exits, so the user confirms again; durable
/// storage is a later unit.
/// </summary>
public sealed class InMemoryProvisionConfirmationStore : IProvisionConfirmationStore
{
    private readonly Dictionary<string, ProvisionConfirmation> _byEnvironment = new(StringComparer.Ordinal);
    private readonly System.Threading.Lock _gate = new();

    /// <inheritdoc />
    public Task<ProvisionConfirmation?> FindAsync(string environmentId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<ProvisionConfirmation?>(_byEnvironment.TryGetValue(environmentId, out var confirmation) ? confirmation : null);
        }
    }

    /// <inheritdoc />
    public Task RecordAsync(ProvisionConfirmation confirmation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmation.EnvironmentId, nameof(confirmation));
        ArgumentOutOfRangeException.ThrowIfZero(confirmation.Actions.Count, nameof(confirmation));
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _byEnvironment[confirmation.EnvironmentId] = confirmation;
        }

        return Task.CompletedTask;
    }
}
