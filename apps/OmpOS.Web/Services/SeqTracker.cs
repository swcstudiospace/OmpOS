namespace OmpOS.Web.Services;

/// <summary>
/// Monotonic Seq de-duplication for one stream session: Seq values arrive in order and resume
/// restarts after the last delivered Seq, so a high-water mark drops replay/live overlap
/// exactly without an ever-growing set.
/// </summary>
internal sealed class SeqTracker
{
    /// <summary>Initializes a new tracker, optionally floored below the first wanted Seq.</summary>
    /// <param name="floor">Seq values at or below this value are treated as already seen.</param>
    internal SeqTracker(long? floor = null)
    {
        HighWater = floor;
    }

    /// <summary>Gets the highest Seq accepted so far, or the floor when nothing arrived yet.</summary>
    internal long? HighWater { get; private set; }

    /// <summary>Accepts <paramref name="seq"/> when it advances the mark.</summary>
    /// <param name="seq">The candidate sequence number.</param>
    /// <returns><see langword="true"/> for a new event; <see langword="false"/> for a replay duplicate.</returns>
    internal bool TryAdd(long seq)
    {
        if (HighWater.HasValue && seq <= HighWater.Value)
        {
            return false;
        }

        HighWater = seq;
        return true;
    }
}
