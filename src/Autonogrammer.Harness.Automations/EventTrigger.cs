namespace Autonogrammer.Harness.Automations;

/// <summary>
/// The automation runs when an event with this name is dispatched. The event's origin is checked by the loop guard.
/// </summary>
public sealed record EventTrigger : AutomationTrigger
{
    /// <summary>
    /// Creates an event trigger.
    /// </summary>
    /// <param name="eventName">The event name to match, compared ordinally.</param>
    /// <exception cref="ArgumentException">The name is null, empty or white space.</exception>
    public EventTrigger(string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        EventName = eventName;
    }

    /// <summary>The event name this trigger matches.</summary>
    public string EventName { get; }
}
