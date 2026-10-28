namespace OmpOS.Web.Services;

/// <summary>The long-lived user-level event stream (<c>GET /events</c>): catalogue changes, session status, stage events and resync.</summary>
internal interface IUserEventFeed
{
    /// <summary>
    /// Streams the user events until <paramref name="cancellationToken"/> is cancelled. A disconnect reconnects with <c>Last-Event-ID</c> set to
    /// the last event identifier seen. A <c>resync</c> event is yielded to the caller, who reloads <c>GET /sessions</c>; the position then starts
    /// over, because the identifiers of another API process are not comparable.
    /// </summary>
    /// <param name="afterId">The last identifier already seen, or null for the live tail.</param>
    /// <param name="cancellationToken">Stops the stream.</param>
    /// <returns>Each unseen event once.</returns>
    IAsyncEnumerable<UserStreamEvent> StreamAsync(long? afterId, CancellationToken cancellationToken);
}
