using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace OmpOS.Coordination.Dragonfly;

/// <summary>The key, channel and value layout of the Dragonfly coordination store. Every key starts with <c>ompos:</c>.</summary>
internal static class DragonflyKeys
{
    private const string Prefix = "ompos:";

    /// <summary>The prefix of a pending idempotency value, followed by the fingerprint.</summary>
    internal const string PendingPrefix = "P|";

    /// <summary>The prefix of a completed idempotency value, followed by the fingerprint, a pipe, and the response.</summary>
    internal const string CompletedPrefix = "C|";

    /// <summary>Gets the pub/sub channel that carries session wake-up signals.</summary>
    public static RedisChannel SignalChannel { get; } = RedisChannel.Literal(Prefix + "signals");

    /// <summary>Returns the run-lease key for a session.</summary>
    public static RedisKey RunLease(string sessionId)
    {
        return Prefix + "lease:run:" + sessionId;
    }

    /// <summary>Returns the idempotency key for a scope and key. The hash keeps arbitrary client keys out of the key space.</summary>
    public static RedisKey Idempotency(string scope, string key)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(scope + "\n" + key));
        return Prefix + "idem:" + Convert.ToHexStringLower(digest);
    }

    /// <summary>Returns the value stored while a claim is pending.</summary>
    public static string PendingValue(string fingerprint)
    {
        return PendingPrefix + fingerprint;
    }

    /// <summary>Returns the value stored once a response is completed.</summary>
    public static string CompletedValue(string fingerprint, string responseJson)
    {
        return CompletedPrefix + fingerprint + "|" + responseJson;
    }
}
