using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace OmpOS.Native.Settings.Models;

/// <summary>
/// A read-only environment that is a plan's environment plus the provider keys as they are when the view is read. It is what the ACP
/// client of a session holds, so omp started later sees a key saved after the session was composed. A key replaces a variable of the same
/// name. The view prints variable names only.
/// </summary>
internal sealed class ProviderKeyOverlay : IReadOnlyDictionary<string, string>
{
    private readonly ProviderKeyEnvironment _keys;
    private readonly IReadOnlyDictionary<string, string> _environment;

    /// <summary>Creates the view.</summary>
    /// <param name="keys">The keys to add.</param>
    /// <param name="environment">The environment they are added to.</param>
    public ProviderKeyOverlay(ProviderKeyEnvironment keys, IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(environment);
        _keys = keys;
        _environment = environment;
    }

    /// <inheritdoc />
    public int Count => _keys.Merge(_environment).Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => _keys.Merge(_environment).Keys;

    /// <inheritdoc />
    public IEnumerable<string> Values => _keys.Merge(_environment).Values;

    /// <inheritdoc />
    public string this[string key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException("The environment has no variable with this name.");

    /// <inheritdoc />
    public bool ContainsKey(string key) => TryGetValue(key, out _);

    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _keys.Current.TryGetValue(key, out value) || _environment.TryGetValue(key, out value);
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _keys.Merge(_environment).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Names the variables, never a value.</summary>
    /// <returns>The variable names.</returns>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{nameof(ProviderKeyOverlay)} [{string.Join(", ", Keys.Order(StringComparer.Ordinal))}]");
}
