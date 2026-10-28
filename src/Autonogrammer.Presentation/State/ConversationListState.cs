namespace Autonogrammer.Presentation.State;

/// <summary>
/// The load state of the session list (UI-SPEC discretion 13). The list starts in <see cref="Loading"/>, then
/// becomes <see cref="Loaded"/> with its conversations, or <see cref="Failed"/> when the session list cannot load.
/// </summary>
public enum ConversationListState
{
    /// <summary>The session list is loading; the view shows a loading placeholder.</summary>
    Loading = 0,

    /// <summary>The session list loaded; the view shows its conversations, or the empty prompt when there are none.</summary>
    Loaded = 1,

    /// <summary>The session list could not load; the view shows the failure and the way to recover.</summary>
    Failed = 2,
}
