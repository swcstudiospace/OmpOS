namespace Autonogrammer.Resilience;

/// <summary>
/// A non-blocking concurrency cap: a call either gets one of <see cref="Capacity"/> slots at once or is refused, so a slow dependency can
/// never pile up an unbounded queue of waiting callers or take connections away from the other operation class.
/// </summary>
internal sealed class Bulkhead
{
    private int _inFlight;
    private long _rejected;

    /// <summary>Initializes a new instance of the <see cref="Bulkhead"/> class.</summary>
    /// <param name="capacity">How many calls may be in flight.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public Bulkhead(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);
        Capacity = capacity;
    }

    /// <summary>Gets how many calls may be in flight.</summary>
    public int Capacity { get; }

    /// <summary>Gets how many calls hold a slot right now.</summary>
    public int InFlight => Volatile.Read(ref _inFlight);

    /// <summary>Gets how many calls were refused since the bulkhead was created.</summary>
    public long Rejected => Interlocked.Read(ref _rejected);

    /// <summary>Takes a slot when one is free.</summary>
    /// <returns><see langword="true"/> when the caller holds a slot and has to call <see cref="Exit"/>; <see langword="false"/> when the bulkhead is full.</returns>
    public bool TryEnter()
    {
        while (true)
        {
            var current = Volatile.Read(ref _inFlight);
            if (current >= Capacity)
            {
                _ = Interlocked.Increment(ref _rejected);
                return false;
            }

            if (Interlocked.CompareExchange(ref _inFlight, current + 1, current) == current)
            {
                return true;
            }
        }
    }

    /// <summary>Gives a slot back.</summary>
    public void Exit() => _ = Interlocked.Decrement(ref _inFlight);
}
