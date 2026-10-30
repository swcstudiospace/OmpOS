using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>The thresholds and head hooks of <see cref="SessionCatalogModel"/>.</summary>
public sealed class SessionCatalogOptions
{
    /// <summary>Gets the age after which a Running session counts as long-running. The default is 1 hour.</summary>
    public TimeSpan LongRunningAfter { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Gets the silence after which a Running session counts as stale. The default is 45 seconds.</summary>
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>Gets how often <see cref="SessionCatalogModel.StartRefreshing"/> refreshes the relative times. The default is 30 seconds.</summary>
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the lookup of the live runtime status of a session (a pending approval, an awaited message), or null when the head reports
    /// none. It is called on the context that refreshes the model.
    /// </summary>
    public Func<string, RuntimeStatus?>? LiveStatus { get; init; }

    /// <summary>
    /// Gets the lookup that says whether the operator closed a session, so no engine is attached, or null when no session can be closed.
    /// </summary>
    public Func<string, bool>? IsClosed { get; init; }

    /// <summary>
    /// Gets the hook that runs an action on the context that owns the models, or null to run it inline. A catalog raises its change
    /// event on any thread, so a UI head passes its dispatcher here.
    /// </summary>
    public Action<Action>? Dispatch { get; init; }
}
