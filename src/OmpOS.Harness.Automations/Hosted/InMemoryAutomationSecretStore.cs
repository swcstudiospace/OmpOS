namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// An <see cref="IAutomationSecretStore"/> that keeps secrets in memory. A restart loses them, so a host that must keep webhooks working
/// across restarts uses <see cref="FileAutomationSecretStore"/> or its own vault-backed store.
/// </summary>
public sealed class InMemoryAutomationSecretStore : IAutomationSecretStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<string?> GetAsync(string reference, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        lock (_gate)
        {
            return ValueTask.FromResult(_secrets.TryGetValue(reference, out var value) ? value : null);
        }
    }

    /// <inheritdoc />
    public ValueTask SetAsync(string reference, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrEmpty(value);
        lock (_gate)
        {
            _secrets[reference] = value;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(string reference, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        lock (_gate)
        {
            _ = _secrets.Remove(reference);
        }

        return ValueTask.CompletedTask;
    }
}
