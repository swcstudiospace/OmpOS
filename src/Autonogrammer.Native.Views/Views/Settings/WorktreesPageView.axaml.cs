using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Worktrees page: read-only facts about run worktrees and the setup commands of <c>.cursor/worktrees.json</c> with whether they are approved.
/// The Refresh button reads the facts again; nothing else on the page acts.
/// </summary>
internal partial class WorktreesPageView : SettingsPageViewBase
{
    /// <summary>Creates a view with no page.</summary>
    public WorktreesPageView()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets the Refresh button.</summary>
    internal Button Refresher => RefreshButton;

    /// <summary>Gets the line that says what the setup state means.</summary>
    internal TextBlock SetupState => SetupStateLine;

    /// <summary>Gets the line that shows why the setup could not be read.</summary>
    internal TextBlock SetupError => SetupErrorHeading;

    /// <summary>Gets the list of setup commands.</summary>
    internal ItemsControl Commands => CommandRows;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (Page is { } page)
        {
            // A load reports its expected failures in the page's state and never throws them, so the task is not observed here.
            _ = page.LoadAsync(CancellationToken.None);
        }
    }
}
