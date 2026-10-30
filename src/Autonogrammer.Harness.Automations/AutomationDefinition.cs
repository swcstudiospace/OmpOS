namespace Autonogrammer.Harness.Automations;

/// <summary>
/// An automation: its identifier, what starts it, the plugin hook whose consent gates its runs, its maximum runtime, its
/// optional rate limit and its body. An automation is registered disabled and runs only after it is enabled.
/// </summary>
public sealed record AutomationDefinition
{
    /// <summary>
    /// Creates an automation definition.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="trigger">What starts the automation.</param>
    /// <param name="consentHookId">The plugin hook whose exact command must be consented to before the automation runs.</param>
    /// <param name="maxRuntime">The longest a single run may last. A run that exceeds it is cancelled and recorded.</param>
    /// <param name="body">The work the automation performs.</param>
    /// <param name="rateLimit">The cap on admitted runs per window, or null for no cap.</param>
    /// <exception cref="ArgumentException">The identifier or consent hook is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException">The trigger or body is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum runtime is not positive.</exception>
    public AutomationDefinition(
        string id,
        AutomationTrigger trigger,
        string consentHookId,
        TimeSpan maxRuntime,
        AutomationBody body,
        RateLimit? rateLimit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentException.ThrowIfNullOrWhiteSpace(consentHookId);
        ArgumentNullException.ThrowIfNull(body);
        if (maxRuntime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRuntime), "The maximum runtime must be positive.");
        }

        Id = id;
        Trigger = trigger;
        ConsentHookId = consentHookId;
        MaxRuntime = maxRuntime;
        Body = body;
        RateLimit = rateLimit;
    }

    /// <summary>The unique identifier.</summary>
    public string Id { get; }

    /// <summary>What starts the automation.</summary>
    public AutomationTrigger Trigger { get; }

    /// <summary>The plugin hook whose consent gates the automation.</summary>
    public string ConsentHookId { get; }

    /// <summary>The longest a single run may last.</summary>
    public TimeSpan MaxRuntime { get; }

    /// <summary>The work the automation performs.</summary>
    public AutomationBody Body { get; }

    /// <summary>The cap on admitted runs per window, or null for no cap.</summary>
    public RateLimit? RateLimit { get; }
}
