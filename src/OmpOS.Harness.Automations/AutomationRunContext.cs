namespace OmpOS.Harness.Automations;

/// <summary>
/// The context a body receives for one run. It emits events whose origin is this automation, so the loop guard can see
/// which run produced them.
/// </summary>
public sealed class AutomationRunContext
{
    private readonly Func<AutomationEvent, CancellationToken, Task<IReadOnlyList<AutomationRunRecord>>> _dispatch;

    internal AutomationRunContext(
        string automationId,
        int depth,
        Func<AutomationEvent, CancellationToken, Task<IReadOnlyList<AutomationRunRecord>>> dispatch)
    {
        AutomationId = automationId;
        Depth = depth;
        _dispatch = dispatch;
    }

    /// <summary>The identifier of the automation running this body.</summary>
    public string AutomationId { get; }

    /// <summary>The chain depth of this run.</summary>
    public int Depth { get; }

    /// <summary>
    /// Emits a named event with this run as its origin and dispatches it to every automation subscribed to the name.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>The records of every triggered attempt, including any the guards blocked or refused.</returns>
    /// <exception cref="ArgumentException"><paramref name="eventName"/> is null, empty or white space.</exception>
    public Task<IReadOnlyList<AutomationRunRecord>> EmitAsync(string eventName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        return _dispatch(new AutomationEvent(eventName, AutomationId, Depth), cancellationToken);
    }
}
