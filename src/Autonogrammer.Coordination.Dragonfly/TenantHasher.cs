using System.Security.Cryptography;
using System.Text;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// <see cref="ITenantHasher"/> over HMAC-SHA256 keyed with the deployment pepper. The result is the first 16 bytes as 32 lowercase hex characters.
/// The input is the authenticated subject; an email or a token is hashed like any other string and never special-cased.
/// </summary>
public sealed class TenantHasher : ITenantHasher
{
    private const int HashBytes = 16;

    private readonly byte[] _key;

    /// <summary>Creates a hasher keyed with the pepper in <paramref name="options"/>.</summary>
    /// <param name="options">The pepper. It must not be blank.</param>
    public TenantHasher(TenantHasherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Pepper, nameof(options));
        _key = Encoding.UTF8.GetBytes(options.Pepper);
    }

    /// <summary>Hashes an account id into its tenant segment.</summary>
    /// <param name="accountId">The authenticated subject. It must not be blank.</param>
    /// <returns>32 lowercase hexadecimal characters.</returns>
    public string HashTenant(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(accountId));
        return Convert.ToHexStringLower(mac.AsSpan(0, HashBytes));
    }
}
