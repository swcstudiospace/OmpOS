using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace OmpOS.Harness.Secrets;

/// <summary>
/// Holds a sensitive text value, such as a provider key, and never renders it. The value leaves the type only through
/// <see cref="Reveal"/>, which is the single member to search for when auditing where a secret is used.
/// </summary>
/// <remarks>
/// The type guarantees that <see cref="ToString"/>, string interpolation, string formatting, equality and JSON
/// serialisation never produce the value. Equality reveals only whether two secrets hold the same text, and
/// <see cref="GetHashCode"/> reveals neither the value nor its length. It does not guarantee that the value is absent
/// from process memory: strings are immutable and the runtime may copy them, so the type does not clear the string it
/// holds.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(SecretStringJsonConverter))]
public sealed class SecretString : IEquatable<SecretString>
{
    /// <summary>The text rendered wherever a secret would otherwise appear.</summary>
    public const string RedactionMarker = "[redacted]";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string _value;

    /// <summary>Wraps a sensitive text value.</summary>
    /// <param name="value">The secret. It must not be null or empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    public SecretString(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _value = value;
    }

    /// <summary>Returns the secret text. This is the only member that exposes the value.</summary>
    /// <returns>The value exactly as it was supplied.</returns>
    public string Reveal() => _value;

    /// <summary>Returns <see cref="RedactionMarker"/>. The value is never included.</summary>
    /// <returns>The redaction marker.</returns>
    public override string ToString() => RedactionMarker;

    /// <summary>
    /// Compares two secrets by the SHA-256 digests of their UTF-16 code units. Two secrets are equal only when their code
    /// units are identical, so an unpaired surrogate is not equal to U+FFFD.
    /// </summary>
    /// <remarks>
    /// The final comparison is a fixed-time comparison of two 32-byte digests. It does not depend on where the values
    /// differ and does not depend on their lengths. Hashing reads each value once, so the time spent hashing grows with
    /// the length of the value: length can be observed from the timing of that step, though it is never compared directly.
    /// </remarks>
    /// <param name="other">The secret to compare with.</param>
    /// <returns>True when both secrets hold the same UTF-16 code units.</returns>
    public bool Equals(SecretString? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        Span<byte> left = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> right = stackalloc byte[SHA256.HashSizeInBytes];
        try
        {
            _ = SHA256.HashData(MemoryMarshal.AsBytes(_value.AsSpan()), left);
            _ = SHA256.HashData(MemoryMarshal.AsBytes(other._value.AsSpan()), right);
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(left);
            CryptographicOperations.ZeroMemory(right);
        }
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SecretString other && Equals(other);

    /// <summary>
    /// Returns a constant, so the hash code reveals neither the value nor its length. Every secret has this hash code.
    /// </summary>
    /// <remarks>
    /// The trade-off: hash-based collections of secrets degrade to linear lookup. A hash derived from the value would
    /// depend on the secret, so the constant avoids that exposure entirely. Secrets are not expected to be dictionary keys.
    /// </remarks>
    /// <returns>Zero, for every secret.</returns>
    public override int GetHashCode() => 0;
}
