using Autonogrammer.Presentation.Hooks;

namespace Autonogrammer.Presentation.Tests.Hooks;

/// <summary>
/// A hooks source the test scripts: it holds the registry in memory, applies enable, move and consent writes to it, counts each
/// write, and can hold a load open or fail a load or a write with the expected failure.
/// </summary>
internal sealed class ScriptedHooksSource : IHooksSource
{
    private readonly List<HookEntry> _hooks = [];
    private readonly TaskCompletionSource _loadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Creates a source whose load completes at once.</summary>
    /// <param name="hooks">The hooks in run order.</param>
    internal ScriptedHooksSource(params HookEntry[] hooks)
    {
        _hooks.AddRange(hooks);
        _loadGate.SetResult();
    }

    /// <summary>Creates a source whose load stays pending until <see cref="CompleteLoad"/> is called.</summary>
    /// <param name="holdLoad">True to hold the load open.</param>
    /// <param name="hooks">The hooks in run order.</param>
    internal ScriptedHooksSource(bool holdLoad, params HookEntry[] hooks)
    {
        _hooks.AddRange(hooks);
        if (!holdLoad)
        {
            _loadGate.SetResult();
        }
    }

    /// <summary>Gets or sets the reason the next loads fail with, or null when they succeed.</summary>
    internal string? LoadFailure { get; set; }

    /// <summary>Gets or sets the reason the next writes fail with, or null when they succeed.</summary>
    internal string? WriteFailure { get; set; }

    /// <summary>Gets the number of writes that reached the source: enable, disable, move and consent.</summary>
    internal int WriteCount { get; private set; }

    /// <summary>Gets the hook identifiers in the order the registry holds them.</summary>
    internal IReadOnlyList<string> Order => _hooks.Select(hook => hook.Id).ToArray();

    /// <summary>Gets the registry's entry for a hook.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <returns>The entry.</returns>
    internal HookEntry Entry(string hookId) => _hooks.Single(hook => string.Equals(hook.Id, hookId, StringComparison.Ordinal));

    /// <summary>Lets a held load complete.</summary>
    internal void CompleteLoad() => _loadGate.TrySetResult();

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await _loadGate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        if (LoadFailure is not null)
        {
            throw new HooksSourceException(LoadFailure);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HookEntry> Snapshot() => [.. _hooks];

    /// <inheritdoc />
    public Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken)
    {
        Write();
        Replace(hookId, entry => entry with { IsEnabled = enabled });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MoveAsync(string hookId, int index, CancellationToken cancellationToken)
    {
        Write();
        var entry = Entry(hookId);
        _hooks.Remove(entry);
        _hooks.Insert(index, entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task GrantConsentAsync(string hookId, CancellationToken cancellationToken)
    {
        Write();
        Replace(hookId, entry => entry with { IsConsented = true });
        return Task.CompletedTask;
    }

    private void Write()
    {
        if (WriteFailure is not null)
        {
            throw new HooksSourceException(WriteFailure);
        }

        WriteCount++;
    }

    private void Replace(string hookId, Func<HookEntry, HookEntry> change)
    {
        var index = _hooks.FindIndex(hook => string.Equals(hook.Id, hookId, StringComparison.Ordinal));
        _hooks[index] = change(_hooks[index]);
    }
}
