namespace Autonogrammer.Harness.Automations;

/// <summary>
/// A named event and the origin that emitted it.
/// </summary>
public sealed record AutomationEvent
{
    /// <summary>
    /// Creates an event.
    /// </summary>
    /// <param name="name">The event name.</param>
    /// <param name="origin">
    /// Where the event came from: the identifier of the automation whose run emitted it, or the name of an external source.
    /// An event whose origin is the automation it would trigger is a self-trigger and is blocked.
    /// </param>
    /// <param name="emittedAtDepth">The chain depth of the run that emitted the event. External events use 0.</param>
    /// <exception cref="ArgumentException">The name or origin is null, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The emitting depth is negative.</exception>
    public AutomationEvent(string name, string origin, int emittedAtDepth = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        ArgumentOutOfRangeException.ThrowIfNegative(emittedAtDepth);
        Name = name;
        Origin = origin;
        EmittedAtDepth = emittedAtDepth;
    }

    /// <summary>The event name.</summary>
    public string Name { get; }

    /// <summary>Where the event came from.</summary>
    public string Origin { get; }

    /// <summary>The chain depth of the emitting run; 0 for an external source.</summary>
    public int EmittedAtDepth { get; }
}
