using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Native.Views.Workspace;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Workspaces;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Workspaces page: the shared folder editor of the current set (without its own Start and Done buttons, because the Settings footer saves),
/// the inline save error, a polite live region for what the last action did, and the recent sets with Load into editor and Forget. Nothing
/// is saved, loaded or forgotten except by a button.
/// </summary>
internal partial class WorkspacesPageView : SettingsPageViewBase
{
    private const string LoadClass = "recent-load";

    private WorkspacesSettingsPage? _page;

    /// <summary>Creates a view with no page.</summary>
    public WorkspacesPageView()
    {
        InitializeComponent();
        PickerHost.ShowFooter = false;
        Start();
    }

    /// <summary>Gets the embedded folder editor.</summary>
    internal WorkspaceSetPickerView Editor => PickerHost;

    /// <summary>Gets the list of recent sets.</summary>
    internal ItemsControl Recents => RecentRows;

    /// <summary>Gets the text shown when no set was saved yet.</summary>
    internal TextBlock NoRecents => NoRecentsText;

    /// <summary>Gets the polite live region that says what the last action did.</summary>
    internal TextBlock Announcement => AnnouncementText;

    /// <summary>Gets the assertive heading of the inline save error.</summary>
    internal TextBlock SaveFailed => SaveFailedHeading;

    /// <summary>Gets the text of the inline save error.</summary>
    internal TextBlock SaveFailedDetail => SaveFailedBody;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    /// <inheritdoc />
    protected override void OnPageChanged(ISettingsPage? page) => _page = page as WorkspacesSettingsPage;

    private void OnLoadRecentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: WorkspaceRecentRow row } && _page is { } page)
        {
            _ = page.LoadRecentIntoEditor(row.Id);
        }
    }

    private void OnForgetRecentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: WorkspaceRecentRow row } && _page is { } page)
        {
            _ = ForgetAsync(page, row);
        }
    }

    // The button that was pressed leaves with its row, so the focus moves to the Load button now at the same place, or to the editor's Add folder when no set is left.
    private async Task ForgetAsync(WorkspacesSettingsPage page, WorkspaceRecentRow row)
    {
        var index = Math.Max(0, page.Recents.ToList().IndexOf(row));
        _ = await page.ForgetRecentAsync(row.Id, CancellationToken.None).ConfigureAwait(true);
        Dispatcher.UIThread.Post(
            () =>
            {
                var loads = RecentRows.GetVisualDescendants().OfType<Button>().Where(static button => button.Classes.Contains(LoadClass)).ToList();
                if (loads.Count > 0)
                {
                    loads[Math.Min(index, loads.Count - 1)].FocusWhenReady();
                }
                else
                {
                    PickerHost.AddFolder.FocusWhenReady();
                }
            },
            DispatcherPriority.Loaded);
    }
}
