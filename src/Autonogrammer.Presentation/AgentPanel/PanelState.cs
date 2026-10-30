namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>The state of the whole Agent Panel, shown by its state view.</summary>
public enum PanelState
{
    /// <summary>There are no agent sessions. The panel shows the empty copy and the New agent button.</summary>
    Empty = 0,

    /// <summary>The panel is initialising, for example checking plugins and restoring agents. It stays usable.</summary>
    Loading = 1,

    /// <summary>At least one agent session exists and the panel loaded.</summary>
    Ready = 2,

    /// <summary>Loading the agents failed. The banner says why and offers a retry.</summary>
    Error = 3,
}
