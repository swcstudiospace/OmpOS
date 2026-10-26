namespace OmpOS.Coordination.Dragonfly;

/// <summary>
/// The Lua scripts that make lease renewal, lease release, idempotency release and idempotency completion atomic. Each script touches only <c>KEYS[1]</c>, the only key
/// declared to the server. Dragonfly rejects undeclared keys.
/// </summary>
internal static class DragonflyScripts
{
    /// <summary>Extends <c>KEYS[1]</c> to <c>ARGV[2]</c> milliseconds, but only while it holds <c>ARGV[1]</c>. Returns 1 when extended, otherwise 0.</summary>
    public const string RenewLease = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PEXPIRE', KEYS[1], ARGV[2]) end return 0";

    /// <summary>Deletes <c>KEYS[1]</c>, but only while it holds <c>ARGV[1]</c>. Returns the number of keys deleted.</summary>
    public const string DeleteIfValue = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) end return 0";

    /// <summary>Writes <c>ARGV[2]</c> with a millisecond lifetime <c>ARGV[3]</c>, but only while <c>KEYS[1]</c> holds <c>ARGV[1]</c>. Returns 1 when written, otherwise 0.</summary>
    public const string CompleteIfPending = "if redis.call('GET', KEYS[1]) == ARGV[1] then redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3]) return 1 end return 0";
}
