using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Sessions;

namespace OmpOS.Native.Agents;

/// <summary>
/// The session catalog the panel's rail reads. It is empty until a registry is attached, and it follows a new registry when the head
/// reloads, so the rail model is built once and never has to know that the registry behind it was replaced.
/// </summary>
internal sealed class ReloadableSessionCatalog : ISessionCatalog
{
    private readonly System.Threading.Lock _gate = new();
    private AgentSessionRegistry? _registry;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IReadOnlyList<SessionSummary> Snapshot()
    {
        AgentSessionRegistry? registry;
        lock (_gate)
        {
            registry = _registry;
        }

        return registry?.Snapshot() ?? [];
    }

    /// <summary>Follows <paramref name="registry"/> from now on, stops following the one it replaces, and raises <see cref="Changed"/>.</summary>
    /// <param name="registry">The registry to read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public void Attach(AgentSessionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        AgentSessionRegistry? previous;
        lock (_gate)
        {
            previous = _registry;
            _registry = registry;
        }

        if (previous is not null)
        {
            previous.Changed -= OnRegistryChanged;
        }

        registry.Changed += OnRegistryChanged;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops following the registry. The catalog is empty afterwards.</summary>
    public void Detach()
    {
        AgentSessionRegistry? previous;
        lock (_gate)
        {
            previous = _registry;
            _registry = null;
        }

        if (previous is not null)
        {
            previous.Changed -= OnRegistryChanged;
        }
    }

    private void OnRegistryChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
