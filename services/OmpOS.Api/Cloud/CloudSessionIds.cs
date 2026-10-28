namespace OmpOS.Api.Cloud;

/// <summary>
/// The identity of the session that holds a cloud agent's event log. The id carries a fixed prefix so any API instance can tell, from the id alone
/// and without a store lookup, that a run belongs to a cloud agent and must go to its worker: a run of such a session is never started locally.
/// Sessions made by <c>POST /sessions</c> get a GUID id, so a caller cannot forge the prefix.
/// </summary>
internal static class CloudSessionIds
{
    /// <summary>The prefix of every cloud agent session id.</summary>
    public const string Prefix = "cloud-";

    /// <summary>Creates a new session id for a cloud agent. It is 38 characters of <c>[a-z0-9-]</c>, valid as a worker token subject.</summary>
    /// <returns>The session id.</returns>
    public static string New() => Prefix + Guid.NewGuid().ToString("N");

    /// <summary>Returns whether <paramref name="sessionId"/> names a cloud agent session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>True when it carries the cloud prefix.</returns>
    public static bool IsCloud(string? sessionId) => sessionId is not null && sessionId.StartsWith(Prefix, StringComparison.Ordinal);
}
