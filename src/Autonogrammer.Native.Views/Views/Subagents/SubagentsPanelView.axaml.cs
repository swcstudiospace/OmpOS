using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Subagents;

/// <summary>
/// The sub-agent panel: one row per ACP task call, with status, elapsed time, the last plain-text line and the running count.
/// A live region announces status changes. The panel states that it is limited to ACP.
/// </summary>
public partial class SubagentsPanelView : UserControl
{
    /// <summary>Creates the view.</summary>
    public SubagentsPanelView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the ACP-only notice.</summary>
    public TextBlock AcpOnlyNotice => AcpNotice;

    /// <summary>Gets the names-unavailable notice.</summary>
    public TextBlock NamesUnavailableNotice => NamesNotice;

    /// <summary>Gets the running-count line.</summary>
    public TextBlock RunningCountText => RunningCount;

    /// <summary>Gets the status live region.</summary>
    public TextBlock StatusLiveRegion => StatusLive;

    /// <summary>Gets the row list.</summary>
    public ItemsControl RowList => Rows;
}
