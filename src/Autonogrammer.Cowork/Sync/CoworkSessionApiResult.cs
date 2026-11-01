namespace Autonogrammer.Cowork.Sync;

/// <summary>The outcome of one Cowork session API call.</summary>
/// <param name="Succeeded">True when the server accepted the call.</param>
/// <param name="StatusCode">The HTTP status, or null when the call never reached a status.</param>
/// <param name="Offline">True when the call could not reach the server.</param>
/// <param name="Reason">A short reason for a failure, or null on success.</param>
public sealed record CoworkSessionApiResult(bool Succeeded, int? StatusCode, bool Offline, string? Reason)
{
    /// <summary>A call the server accepted.</summary>
    public static CoworkSessionApiResult Accepted { get; } = new(true, 200, false, null);

    /// <summary>A conflict. Local work is kept and the call is not retried.</summary>
    /// <param name="reason">The reason shown with the Not synced mark.</param>
    /// <returns>The result.</returns>
    public static CoworkSessionApiResult Conflict(string reason) => new(false, 409, false, reason);

    /// <summary>A call that did not reach the server. It is retried with capped backoff.</summary>
    /// <param name="reason">The reason shown with the Not synced mark.</param>
    /// <returns>The result.</returns>
    public static CoworkSessionApiResult Unreachable(string reason) => new(false, null, true, reason);
}
