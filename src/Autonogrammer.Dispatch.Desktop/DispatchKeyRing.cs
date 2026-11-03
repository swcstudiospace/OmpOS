using System.Security.Cryptography;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// The hub signing keys this desktop accepts, keyed by <c>kid</c>. A kid outside this set is refused. The ring is built only from
/// the keys delivered at pairing.
/// </summary>
public sealed class DispatchKeyRing : IDisposable
{
    private readonly Dictionary<string, ECDsa> _keys = new(StringComparer.Ordinal);

    /// <summary>Imports every key. A key that is not a valid P-256 SubjectPublicKeyInfo is refused.</summary>
    /// <param name="keys">The pairing keys.</param>
    /// <exception cref="ArgumentException">A key is malformed or a kid repeats.</exception>
    public DispatchKeyRing(IEnumerable<DispatchPublicKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var entry in keys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Kid, nameof(keys));
            if (_keys.ContainsKey(entry.Kid))
            {
                throw new ArgumentException("A kid appears more than once.", nameof(keys));
            }

            var der = Convert.FromBase64String(entry.SpkiBase64);
            var key = ECDsa.Create();
            try
            {
                key.ImportSubjectPublicKeyInfo(der, out _);
            }
            catch (CryptographicException ex)
            {
                key.Dispose();
                throw new ArgumentException("A pairing key is not a valid public key.", nameof(keys), ex);
            }

            _keys.Add(entry.Kid, key);
        }
    }

    /// <summary>Gets the number of accepted kids.</summary>
    public int Count => _keys.Count;

    /// <summary>Looks up the public key of a kid.</summary>
    /// <param name="kid">The key identifier from the frame.</param>
    /// <param name="key">The key when found; null otherwise.</param>
    /// <returns>True when the kid was delivered at pairing.</returns>
    public bool TryGet(string kid, out ECDsa? key) => _keys.TryGetValue(kid, out key);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var key in _keys.Values)
        {
            key.Dispose();
        }

        _keys.Clear();
    }
}
