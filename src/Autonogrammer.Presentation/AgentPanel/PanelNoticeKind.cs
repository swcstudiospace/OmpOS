namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>Which notice the thread header shows for a session. Errors are not notices: they have their own assertive region.</summary>
public enum PanelNoticeKind
{
    /// <summary>No notice.</summary>
    None = 0,

    /// <summary>One or more pipeline stages are skipped, degraded, failed or unavailable. The agent keeps running.</summary>
    Degraded = 1,

    /// <summary>No plugin is available. The agent runs as plain omp.</summary>
    NoPlugins = 2,

    /// <summary>The session was running when the application closed. It offers Resume.</summary>
    Lost = 3,

    /// <summary>The session was resumed. It stays until dismissed.</summary>
    Resumed = 4,
}
