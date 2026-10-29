using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// The one builder of every Dragonfly key and channel: <c>autonogrammer:v1:&lt;domain&gt;:&lt;tenant&gt;:&lt;entity&gt;[:&lt;id&gt;]</c> with the domains
/// <c>lease</c>, <c>idem</c>, <c>presence</c>, <c>rl</c>, <c>auth</c> and <c>sig</c>. Ids are hashed or charset-validated before they join a key, and
/// every write path asks <see cref="RequireTimeToLive"/> for its lifetime, so no key is written without an expiry of at most 24 hours.
/// </summary>
internal static class DragonflyKeyspace
{
    /// <summary>The prefix of every key and channel.</summary>
    public const string Prefix = "autonogrammer:v1:";

    /// <summary>The prefix of a pending idempotency value, followed by the fingerprint.</summary>
    internal const string PendingPrefix = "P|";

    /// <summary>The prefix of a completed idempotency value, followed by the fingerprint, a pipe, and the response.</summary>
    internal const string CompletedPrefix = "C|";

    private const int TenantLength = 32;
    private const int MaximumSegmentLength = 128;

    /// <summary>Gets the longest lifetime any key may have.</summary>
    public static TimeSpan MaximumTimeToLive { get; } = TimeSpan.FromHours(24);

    /// <summary>Gets the pub/sub channel that carries session wake-ups. The payload is a session id only.</summary>
    public static RedisChannel SignalSession { get; } = RedisChannel.Literal(Prefix + "sig:session");

    /// <summary>Gets the pub/sub channel that carries dispatch wake-ups. The payload is a command id only.</summary>
    public static RedisChannel SignalDispatch { get; } = RedisChannel.Literal(Prefix + "sig:dispatch");

    /// <summary>Returns the run-lease key for a session.</summary>
    /// <param name="sessionId">The session id: letters, digits, underscores and hyphens.</param>
    /// <returns>The key.</returns>
    public static RedisKey LeaseRun(string sessionId)
    {
        return Prefix + "lease:run:" + RequireSegment(sessionId, nameof(sessionId));
    }

    /// <summary>Returns the idempotency key for a scope and key. The hash keeps arbitrary client keys out of the key space.</summary>
    /// <param name="scope">The namespace of the key.</param>
    /// <param name="key">The client-supplied idempotency key.</param>
    /// <returns>The key.</returns>
    public static RedisKey Idempotency(string scope, string key)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(scope + "\n" + key));
        return Prefix + "idem:" + Convert.ToHexStringLower(digest);
    }

    /// <summary>Returns the presence key of one device of a tenant.</summary>
    /// <param name="tenant">The tenant segment: 32 lowercase hexadecimal characters.</param>
    /// <param name="deviceId">The device id: letters, digits, underscores and hyphens.</param>
    /// <returns>The key.</returns>
    public static RedisKey Presence(string tenant, string deviceId)
    {
        return Prefix + "presence:" + RequireTenant(tenant) + ":" + RequireSegment(deviceId, nameof(deviceId));
    }

    /// <summary>Returns the shared rate-limit key of one policy, tenant and window.</summary>
    /// <param name="policy">The policy name: letters, digits, underscores and hyphens.</param>
    /// <param name="tenant">The tenant segment: 32 lowercase hexadecimal characters.</param>
    /// <param name="window">The window identifier: letters, digits, underscores and hyphens.</param>
    /// <returns>The key.</returns>
    public static RedisKey RateLimit(string policy, string tenant, string window)
    {
        return Prefix + "rl:" + RequireSegment(policy, nameof(policy)) + ":" + RequireTenant(tenant) + ":" + RequireSegment(window, nameof(window));
    }

    /// <summary>Returns the key of a device-hello nonce.</summary>
    /// <param name="sha256Hex">The SHA-256 of the nonce: 64 lowercase hexadecimal characters.</param>
    /// <returns>The key.</returns>
    public static RedisKey AuthNonce(string sha256Hex)
    {
        ArgumentNullException.ThrowIfNull(sha256Hex);
        if (sha256Hex.Length != 64 || !IsLowerHex(sha256Hex))
        {
            throw new ArgumentException("The nonce hash must be 64 lowercase hexadecimal characters.", nameof(sha256Hex));
        }

        return Prefix + "auth:nonce:" + sha256Hex;
    }

    /// <summary>Returns the one pattern the erasure scan uses: every key of a tenant, whatever its domain.</summary>
    /// <param name="tenant">The tenant segment: 32 lowercase hexadecimal characters.</param>
    /// <returns>The match pattern.</returns>
    public static string TenantScanPattern(string tenant)
    {
        return Prefix + "*:" + RequireTenant(tenant) + ":*";
    }

    /// <summary>Returns the value stored while a claim is pending.</summary>
    /// <param name="fingerprint">The request fingerprint.</param>
    /// <returns>The stored value.</returns>
    public static string PendingValue(string fingerprint)
    {
        return PendingPrefix + fingerprint;
    }

    /// <summary>Returns the value stored once a response is completed.</summary>
    /// <param name="fingerprint">The request fingerprint.</param>
    /// <param name="responseJson">The response to replay.</param>
    /// <returns>The stored value.</returns>
    public static string CompletedValue(string fingerprint, string responseJson)
    {
        return CompletedPrefix + fingerprint + "|" + responseJson;
    }

    /// <summary>Checks the lifetime of a write: present, positive and at most 24 hours.</summary>
    /// <param name="timeToLive">The requested lifetime, or <see langword="null"/> when the caller supplied none.</param>
    /// <returns>The accepted lifetime.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The lifetime is missing, not positive or above 24 hours.</exception>
    public static TimeSpan RequireTimeToLive(TimeSpan? timeToLive)
    {
        if (timeToLive is not { } value || value <= TimeSpan.Zero || value > MaximumTimeToLive)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), timeToLive, "Every key needs a time to live of more than zero and at most 24 hours.");
        }

        return value;
    }

    /// <summary>Checks a tenant segment before it joins a key or a scan pattern.</summary>
    /// <param name="tenant">The tenant segment.</param>
    /// <returns>The same segment.</returns>
    /// <exception cref="ArgumentException">The segment is not 32 lowercase hexadecimal characters.</exception>
    public static string RequireTenant(string tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (tenant.Length != TenantLength || !IsLowerHex(tenant))
        {
            throw new ArgumentException("The tenant must be 32 lowercase hexadecimal characters.", nameof(tenant));
        }

        return tenant;
    }

    private static string RequireSegment(string value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (value.Length is 0 or > MaximumSegmentLength || !value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            throw new ArgumentException("A key segment must be 1 to 128 letters, digits, underscores or hyphens.", name);
        }

        return value;
    }

    private static bool IsLowerHex(string value)
    {
        return value.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}
