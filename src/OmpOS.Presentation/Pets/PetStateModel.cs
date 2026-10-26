using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Kanban;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Pets;

/// <summary>
/// The companion's state (Phase 41): it maps the runtime status of the sessions to a <see cref="PetState"/>, applies the hide toggle and the
/// motion preference, and exposes the text equivalent. It holds no art and no view; a head binds a companion view to it.
/// </summary>
/// <remarks>
/// <para>
/// The mapping is a table, not a guess: Queued and Running are <see cref="PetState.Working"/>; a pending approval, an awaited reply and a
/// failure are <see cref="PetState.AttentionNeeded"/>; Paused and Completed are <see cref="PetState.Idle"/>; Unreachable and any value this build does not
/// know are <see cref="PetState.Fallback"/>, so the companion never shows a calm face for a status it cannot read.
/// </para>
/// <para>Not thread-safe: it runs on the UI context that owns the presentation models.</para>
/// </remarks>
public sealed class PetStateModel : ObservableModel
{
    private PetState _mappedState = PetState.Idle;
    private PetPreferences _preferences = new();
    private bool _systemPrefersReducedMotion;

    /// <summary>Gets the state the sessions map to, ignoring the hide toggle. It is <see cref="PetState.Idle"/> until a status is set.</summary>
    public PetState MappedState => _mappedState;

    /// <summary>Gets the state to draw: <see cref="PetState.Hidden"/> when the user hid the companion, otherwise <see cref="MappedState"/>.</summary>
    public PetState State => _preferences.IsHidden ? PetState.Hidden : _mappedState;

    /// <summary>Gets a value indicating whether the companion is part of the layout. False when hidden: the view removes it.</summary>
    public bool IsVisible => State != PetState.Hidden;

    /// <summary>Gets a value indicating whether the companion may animate: it is visible and reduced motion is not in force.</summary>
    public bool AnimationEnabled => IsVisible && !IsMotionReduced;

    /// <summary>Gets a value indicating whether reduced motion is in force for the companion.</summary>
    public bool IsMotionReduced => _preferences.IsMotionReduced(_systemPrefersReducedMotion);

    /// <summary>Gets the text equivalent of <see cref="State"/>.</summary>
    public string AccessibleText => PetAccessibleText.TextOf(State);

    /// <summary>Gets the label of the hide toggle for the current preference.</summary>
    public string ToggleLabel => PetAccessibleText.ToggleLabelOf(_preferences.IsHidden);

    /// <summary>Gets or sets the user's preferences.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public PetPreferences Preferences
    {
        get => _preferences;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Apply(() => _preferences = value);
        }
    }

    /// <summary>Gets or sets the operating-system reduced-motion flag, false when the head cannot read it.</summary>
    public bool SystemPrefersReducedMotion
    {
        get => _systemPrefersReducedMotion;
        set => Apply(() => _systemPrefersReducedMotion = value);
    }

    /// <summary>Maps one runtime status to a companion state.</summary>
    /// <param name="status">The runtime status of the session, or null when there is no session.</param>
    /// <returns>The state. No session is <see cref="PetState.Idle"/>; a value outside <see cref="RuntimeStatus"/> is <see cref="PetState.Fallback"/>.</returns>
    public static PetState StateOf(RuntimeStatus? status) => status switch
    {
        null => PetState.Idle,
        RuntimeStatus.Queued => PetState.Working,
        RuntimeStatus.Running => PetState.Working,
        RuntimeStatus.AwaitingInput => PetState.AttentionNeeded,
        RuntimeStatus.NeedsAttention => PetState.AttentionNeeded,
        RuntimeStatus.Failed => PetState.AttentionNeeded,
        RuntimeStatus.Paused => PetState.Idle,
        RuntimeStatus.Completed => PetState.Idle,
        _ => PetState.Fallback,
    };

    /// <summary>Maps the status a panel row shows to a companion state.</summary>
    /// <param name="status">The row status.</param>
    /// <returns>The state. <see cref="AgentRowStatus.Unknown"/>, <see cref="AgentRowStatus.Lost"/> and any undefined value are <see cref="PetState.Fallback"/>.</returns>
    public static PetState StateOf(AgentRowStatus status) => status switch
    {
        AgentRowStatus.NeedsApproval => PetState.AttentionNeeded,
        AgentRowStatus.WaitingForYou => PetState.AttentionNeeded,
        AgentRowStatus.Failed => PetState.AttentionNeeded,
        AgentRowStatus.Running => PetState.Working,
        AgentRowStatus.Queued => PetState.Working,
        AgentRowStatus.Ready => PetState.Idle,
        AgentRowStatus.Paused => PetState.Idle,
        AgentRowStatus.Closed => PetState.Idle,
        AgentRowStatus.Completed => PetState.Idle,
        _ => PetState.Fallback,
    };

    /// <summary>Combines the states of several sessions into the one the companion shows.</summary>
    /// <param name="states">The per-session states.</param>
    /// <returns>
    /// The most urgent state: <see cref="PetState.AttentionNeeded"/>, then <see cref="PetState.Working"/>, then <see cref="PetState.Fallback"/>, then
    /// <see cref="PetState.Idle"/>. No session is <see cref="PetState.Idle"/>. <see cref="PetState.Hidden"/> is a preference, not a session state, and counts as idle.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="states"/> is null.</exception>
    public static PetState Combine(IEnumerable<PetState> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        var result = PetState.Idle;
        foreach (var state in states)
        {
            if (UrgencyOf(state) > UrgencyOf(result))
            {
                result = state;
            }
        }

        return result;
    }

    private static int UrgencyOf(PetState state) => state switch
    {
        PetState.AttentionNeeded => 3,
        PetState.Working => 2,
        PetState.Fallback => 1,
        _ => 0,
    };

    /// <summary>Sets the state from one runtime status.</summary>
    /// <param name="status">The runtime status, or null when there is no session.</param>
    public void SetRuntimeStatus(RuntimeStatus? status) => SetMappedState(StateOf(status));

    /// <summary>Sets the state from the status a panel row shows.</summary>
    /// <param name="status">The row status.</param>
    public void SetRowStatus(AgentRowStatus status) => SetMappedState(StateOf(status));

    /// <summary>Sets the state from the statuses of every session, using <see cref="Combine"/>.</summary>
    /// <param name="statuses">The row statuses of the sessions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="statuses"/> is null.</exception>
    public void SetRowStatuses(IEnumerable<AgentRowStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        SetMappedState(Combine(statuses.Select(StateOf)));
    }

    /// <summary>Sets the hide toggle.</summary>
    /// <param name="isHidden">True to remove the companion, false to show it.</param>
    public void SetHidden(bool isHidden) => Preferences = _preferences with { IsHidden = isHidden };

    /// <summary>Flips the hide toggle.</summary>
    public void ToggleHidden() => SetHidden(!_preferences.IsHidden);

    private void SetMappedState(PetState state) => Apply(() => _mappedState = state);

    /// <summary>Runs one mutation and announces every derived property that changed with it.</summary>
    /// <param name="mutate">The mutation of one backing field.</param>
    private void Apply(Action mutate)
    {
        var before = Snapshot();
        mutate();
        var after = Snapshot();

        if (before.Mapped != after.Mapped)
        {
            NotifyPropertyChanged(nameof(MappedState));
        }

        if (before.State != after.State)
        {
            NotifyPropertyChanged(nameof(State));
            NotifyPropertyChanged(nameof(AccessibleText));
        }

        if (before.Visible != after.Visible)
        {
            NotifyPropertyChanged(nameof(IsVisible));
        }

        if (before.Reduced != after.Reduced)
        {
            NotifyPropertyChanged(nameof(IsMotionReduced));
        }

        if (before.Animated != after.Animated)
        {
            NotifyPropertyChanged(nameof(AnimationEnabled));
        }

        if (before.Preferences != after.Preferences)
        {
            NotifyPropertyChanged(nameof(Preferences));
            NotifyPropertyChanged(nameof(ToggleLabel));
        }

        if (before.SystemFlag != after.SystemFlag)
        {
            NotifyPropertyChanged(nameof(SystemPrefersReducedMotion));
        }
    }

    private Derived Snapshot() => new(_mappedState, State, IsVisible, IsMotionReduced, AnimationEnabled, _preferences, _systemPrefersReducedMotion);

    private readonly record struct Derived(
        PetState Mapped,
        PetState State,
        bool Visible,
        bool Reduced,
        bool Animated,
        PetPreferences Preferences,
        bool SystemFlag);
}
