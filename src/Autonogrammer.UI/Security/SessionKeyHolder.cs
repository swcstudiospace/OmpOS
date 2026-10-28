using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Autonogrammer.UI.Security;

/// <summary>
/// Holds a provider key for one browser session, in memory only. The key is never written to browser storage of any kind
/// (key-value storage, databases or cookies), never serialised, and never rendered: it lives in a private character buffer that is
/// overwritten when the key is cleared or the holder is disposed. A page reload creates a new holder, so the key is gone.
/// </summary>
/// <remarks>
/// <see cref="ToString"/>, string interpolation and JSON serialisation never produce the key; the single member that returns it is
/// <see cref="Reveal"/>. The holder cannot protect strings it has handed out: a string returned by <see cref="Reveal"/> is immutable,
/// the runtime may copy it, and it stays in memory until garbage collected. See <c>docs/runbooks/web-key-threat-model.md</c>.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(SessionKeyHolderJsonConverter))]
public sealed class SessionKeyHolder : IDisposable
{
    /// <summary>The text rendered wherever the key would otherwise appear.</summary>
    public const string RedactionMarker = "[redacted]";

    private readonly Lock _gate = new();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private char[] _buffer = [];

    private bool _disposed;

    /// <summary>Gets a value indicating whether a key is currently held.</summary>
    public bool IsSet
    {
        get
        {
            lock (_gate)
            {
                return !_disposed && _buffer.Length > 0;
            }
        }
    }

    /// <summary>Holds a key, overwriting any key held before.</summary>
    /// <param name="key">The key. It must not be null or empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    /// <exception cref="ObjectDisposedException">The holder was disposed.</exception>
    public void Set(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Array.Clear(_buffer);
            _buffer = key.ToCharArray();
        }
    }

    /// <summary>Returns the held key. This is the only member that exposes it.</summary>
    /// <returns>The key exactly as it was set. The returned string is outside the holder's control and is not cleared.</returns>
    /// <exception cref="InvalidOperationException">No key is held.</exception>
    /// <exception cref="ObjectDisposedException">The holder was disposed.</exception>
    public string Reveal()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer.Length == 0
                ? throw new InvalidOperationException("No session key is held.")
                : new string(_buffer);
        }
    }

    /// <summary>Overwrites and forgets the held key. The holder stays usable and <see cref="Set"/> may hold a new key.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_buffer);
            _buffer = [];
        }
    }

    /// <summary>Overwrites and forgets the held key and refuses every later <see cref="Set"/> and <see cref="Reveal"/>.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            Array.Clear(_buffer);
            _buffer = [];
            _disposed = true;
        }
    }

    /// <summary>Returns <see cref="RedactionMarker"/>. The key is never included.</summary>
    /// <returns>The redaction marker.</returns>
    public override string ToString() => RedactionMarker;
}
