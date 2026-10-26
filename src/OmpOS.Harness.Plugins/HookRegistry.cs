using System.Collections.Immutable;

namespace OmpOS.Harness.Plugins;

/// <summary>
/// The hooks of every loaded plugin, in run order. It lists trigger, command and enabled state, and supports enable,
/// disable, reorder and consent. Enablement, order and consent are persisted through the state store.
/// </summary>
/// <remarks>
/// Reads take a snapshot and never wait. Changes are serialized, and each one is persisted before it becomes visible.
/// </remarks>
public sealed class HookRegistry : IDisposable
{
    private readonly PluginStateStore _state;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ImmutableList<HookRegistration> _hooks = ImmutableList<HookRegistration>.Empty;

    internal HookRegistry(PluginStateStore state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <summary>
    /// Lists every hook in run order, including disabled hooks.
    /// </summary>
    public IReadOnlyList<HookRegistration> List() => _hooks;

    /// <summary>
    /// Returns true when consent is recorded for the hook's exact command, trigger and blocking flag. Editing any of them
    /// changes its hash, so consent for the old one does not apply to the new one.
    /// </summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <exception cref="ArgumentException">No hook has the identifier.</exception>
    public bool IsConsented(string hookId) => _state.IsConsented(Find(hookId).CommandHash);

    /// <summary>
    /// Records consent for the hook's exact command as it is now. The hook can then run until its command changes.
    /// </summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentException">No hook has the identifier.</exception>
    public async Task GrantConsentAsync(string hookId, CancellationToken cancellationToken)
    {
        var hook = Find(hookId);
        await _state.RecordConsentAsync(hook.CommandHash, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Enables or disables a hook. A disabled hook is never started and is not reported in run results.
    /// </summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="enabled">True to enable the hook; false to disable it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentException">No hook has the identifier.</exception>
    public async Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = IndexOf(hookId);
            await _state.SetHookDisabledAsync(hookId, disabled: !enabled, cancellationToken).ConfigureAwait(false);
            _hooks = _hooks.SetItem(index, _hooks[index] with { Enabled = enabled });
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Moves a hook to a new position in run order. The hooks between the old and new positions shift by one.
    /// </summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="index">The zero-based position the hook takes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentException">No hook has the identifier.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the list.</exception>
    public async Task MoveAsync(string hookId, int index, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var from = IndexOf(hookId);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _hooks.Count);

            var reordered = _hooks.RemoveAt(from).Insert(index, _hooks[from]);
            await _state.SetHookOrderAsync(reordered.Select(hook => hook.Id).ToList(), cancellationToken).ConfigureAwait(false);
            _hooks = reordered;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Releases the lock that serializes changes.
    /// </summary>
    public void Dispose() => _gate.Dispose();

    internal async Task RegisterAsync(string pluginId, IReadOnlyList<PluginHookDeclaration> declarations, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(declarations);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hooks.Any(hook => string.Equals(hook.PluginId, pluginId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"The hooks of plugin '{pluginId}' are already registered.");
            }

            var combined = _hooks;
            for (var index = 0; index < declarations.Count; index++)
            {
                var declaration = declarations[index];
                var id = $"{pluginId}#{index}";
                combined = combined.Add(new HookRegistration(
                    id,
                    pluginId,
                    declaration.Trigger,
                    declaration.Command,
                    HookCommandIdentity.ComputeHash(declaration.Trigger, declaration.Blocking, declaration.Command),
                    declaration.Blocking,
                    Enabled: !_state.IsHookDisabled(id)));
            }

            _hooks = ApplyPersistedOrder(combined);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Hooks named in the persisted order keep their place. Hooks it does not name follow, in registration order.
    private ImmutableList<HookRegistration> ApplyPersistedOrder(ImmutableList<HookRegistration> hooks)
    {
        var ranks = _state.HookOrder
            .Select((id, rank) => (id, rank))
            .ToDictionary(entry => entry.id, entry => entry.rank, StringComparer.Ordinal);
        return hooks
            .Select((hook, position) => (hook, key: ranks.TryGetValue(hook.Id, out var rank) ? rank : ranks.Count + position))
            .OrderBy(entry => entry.key)
            .Select(entry => entry.hook)
            .ToImmutableList();
    }

    private HookRegistration Find(string hookId) => _hooks[IndexOf(hookId)];

    private int IndexOf(string hookId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hookId);
        for (var index = 0; index < _hooks.Count; index++)
        {
            if (string.Equals(_hooks[index].Id, hookId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new ArgumentException($"No hook has the identifier '{hookId}'.", nameof(hookId));
    }
}
