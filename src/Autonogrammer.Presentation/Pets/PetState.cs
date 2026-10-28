namespace Autonogrammer.Presentation.Pets;

/// <summary>
/// The one state the companion shows (Phase 41). It is derived from the runtime status of the sessions by <see cref="PetStateModel"/> and never
/// set by a view. Every state has a text equivalent in <see cref="PetAccessibleText"/>: the companion is never the only carrier of a status.
/// </summary>
public enum PetState
{
    /// <summary>The companion is switched off by the user and is not shown at all.</summary>
    Hidden = 0,

    /// <summary>No agent is working and none needs the user.</summary>
    Idle = 1,

    /// <summary>An agent run is queued or executing.</summary>
    Working = 2,

    /// <summary>An agent needs the user: an approval, a reply, or a failure to look at.</summary>
    AttentionNeeded = 3,

    /// <summary>The status is not known or cannot be reached. The companion says so instead of guessing.</summary>
    Fallback = 4,
}
