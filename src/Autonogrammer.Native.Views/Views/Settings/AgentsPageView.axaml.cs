using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Agents page: the terminal auto-run switch was removed (Phase 81), so the page carries only the one-time removal announcement
/// and the note that every tool call follows omp's approval policy. There is no control left to sync.
/// </summary>
internal partial class AgentsPageView : SettingsPageViewBase
{
    /// <summary>Creates a view with no page.</summary>
    public AgentsPageView()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets the one-time announcement that the terminal auto-run switch was removed.</summary>
    internal TextBlock Announcement => AutoRunRemoved;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;
}
