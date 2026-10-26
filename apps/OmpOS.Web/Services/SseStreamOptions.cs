namespace OmpOS.Web.Services;

/// <summary>
/// Reconnect policy for <see cref="SseEventStream"/>.
/// </summary>
internal sealed record SseStreamOptions
{
    /// <summary>Gets the delay before the first reconnect attempt.</summary>
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets the upper bound for the exponential backoff between reconnects.</summary>
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the maximum consecutive reconnect attempts that deliver no event after a mid-stream
    /// disconnect. A connection that delivers an event resets the count. Defaults to 10: a
    /// persistently dead origin throws instead of spinning silently, so the surface can show
    /// the offline state. Tests override this with tighter bounds.
    /// </summary>
    public int? MaxReconnects { get; init; } = 10;
}
