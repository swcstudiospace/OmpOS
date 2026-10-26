using System.Collections.Immutable;
using System.Text.Json;

namespace OmpOS.Harness.Providers;

/// <summary>
/// Every provider the application can use: the built-in <see cref="ProviderCatalogue"/> plus the custom providers the user
/// defined. A custom provider is added from data alone, validated with the same rules as a catalogue row, and persisted
/// through a <see cref="ProviderRegistryStore"/> before it becomes visible, so a failed write leaves the registry as it was.
/// </summary>
public sealed class ProviderRegistry : IDisposable
{
    private readonly ProviderRegistryStore _store;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private ImmutableDictionary<string, CustomProviderDefinition> _custom;

    private ProviderRegistry(ProviderRegistryStore store, ImmutableDictionary<string, CustomProviderDefinition> custom)
    {
        _store = store;
        _custom = custom;
    }

    /// <summary>Gets the built-in entries, then the custom entries ordered by id.</summary>
    public IReadOnlyList<ProviderEntry> Entries =>
        [.. ProviderCatalogue.All, .. CustomProviders.Select(definition => definition.ToEntry())];

    /// <summary>Gets the custom providers, ordered by id.</summary>
    public IReadOnlyList<CustomProviderDefinition> CustomProviders =>
        [.. _custom.Values.OrderBy(definition => definition.Id, StringComparer.Ordinal)];

    /// <summary>
    /// Opens the registry over a store. A store file that exists but cannot be read, or that holds an invalid or colliding
    /// provider, makes the open fail; it is never treated as an empty registry.
    /// </summary>
    /// <param name="store">The store that persists the custom providers.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="JsonException">The stored providers are unreadable, invalid or collide with each other or the catalogue.</exception>
    /// <returns>The opened registry.</returns>
    public static async Task<ProviderRegistry> OpenAsync(ProviderRegistryStore store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var stored = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        var custom = ImmutableDictionary.CreateBuilder<string, CustomProviderDefinition>(StringComparer.Ordinal);
        foreach (var definition in stored)
        {
            var errors = Validate(definition, custom.ContainsKey).Errors;
            if (errors.Count > 0)
            {
                throw new JsonException(
                    $"The provider registry holds an invalid provider '{definition.Id}': {string.Join(", ", errors)}.");
            }

            custom.Add(definition.Id, definition);
        }

        return new ProviderRegistry(store, custom.ToImmutable());
    }

    /// <summary>Releases the lock that serializes writes. The file on disk is already current after every change.</summary>
    public void Dispose() => _writeGate.Dispose();

    /// <summary>Returns the entry with the id, built-in or custom, or null when there is none.</summary>
    /// <param name="id">The provider id.</param>
    public ProviderEntry? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return ProviderCatalogue.Find(id) ?? (_custom.TryGetValue(id, out var definition) ? definition.ToEntry() : null);
    }

    /// <summary>Returns the model ids a custom provider lists. A built-in entry lists none, and so does an unknown id.</summary>
    /// <param name="id">The provider id.</param>
    public IReadOnlyList<string> ModelsFor(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _custom.TryGetValue(id, out var definition) ? definition.Models : [];
    }

    /// <summary>
    /// Validates a definition against the registry as it stands now: the definition's own rules, plus a refusal when the id is
    /// already a catalogue entry or a registered custom provider. The registry is not changed.
    /// </summary>
    /// <param name="definition">The definition to check.</param>
    public ProviderValidationResult ValidateCandidate(CustomProviderDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Validate(definition, _custom.ContainsKey);
    }

    /// <summary>
    /// Adds a custom provider. A definition that breaks any rule is rejected with every error and nothing is written. An
    /// accepted definition is persisted before it is visible.
    /// </summary>
    /// <param name="definition">The provider to add.</param>
    /// <param name="cancellationToken">Cancels the wait for the write lock and the write.</param>
    /// <exception cref="IOException">The file could not be written. The registry is unchanged.</exception>
    /// <returns>The validation result. An empty error list means the provider was added.</returns>
    public async Task<ProviderValidationResult> AddCustomAsync(CustomProviderDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = Validate(definition, _custom.ContainsKey);
            if (!result.IsValid)
            {
                return result;
            }

            var next = _custom.Add(definition.Id, definition);
            await PersistAsync(next, cancellationToken).ConfigureAwait(false);
            _custom = next;
            return result;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>Removes a custom provider. A built-in entry cannot be removed.</summary>
    /// <param name="id">The id of the custom provider.</param>
    /// <param name="cancellationToken">Cancels the wait for the write lock and the write.</param>
    /// <exception cref="IOException">The file could not be written. The registry is unchanged.</exception>
    /// <returns>True when a custom provider was removed; false when there was none with that id.</returns>
    public async Task<bool> RemoveCustomAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_custom.ContainsKey(id))
            {
                return false;
            }

            var next = _custom.Remove(id);
            await PersistAsync(next, cancellationToken).ConfigureAwait(false);
            _custom = next;
            return true;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static ProviderValidationResult Validate(CustomProviderDefinition definition, Func<string, bool> isRegistered)
    {
        var errors = new List<ProviderValidationError>(definition.Validate().Errors);
        if (ProviderCatalogue.Find(definition.Id) is not null || isRegistered(definition.Id))
        {
            errors.Add(ProviderValidationError.IdAlreadyRegistered);
        }

        return new ProviderValidationResult(errors);
    }

    private Task PersistAsync(ImmutableDictionary<string, CustomProviderDefinition> next, CancellationToken cancellationToken) =>
        _store.WriteAsync([.. next.Values.OrderBy(definition => definition.Id, StringComparer.Ordinal)], cancellationToken);
}
