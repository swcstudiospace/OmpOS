namespace Autonogrammer.Presentation.Cloud;

/// <summary>The one state the Cloud agents page is in. The view draws exactly one at a time.</summary>
public enum CloudAgentsViewState
{
    /// <summary>The first load is in progress.</summary>
    Loading = 0,

    /// <summary>Cloud agents are not set up: the page shows the setup steps and no agent controls.</summary>
    NotConfigured = 1,

    /// <summary>The first load failed: the page shows the reason and Try again.</summary>
    Error = 2,

    /// <summary>The load worked and the user has no cloud agent.</summary>
    Empty = 3,

    /// <summary>The load worked and there is at least one agent.</summary>
    Ready = 4,
}
