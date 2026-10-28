using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Hooks;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Views.Hooks;

/// <summary>
/// The Hooks page of the Settings window: the Run hooks switch, the state of the main area (switched off, untrusted folder, invalid files, empty, or
/// the list by scope and file), the consent panel of the selected hook, quarantines, diagnostics and the run log. Every action is a click of a
/// button the user pressed; the view starts no hook and holds no state of its own.
/// </summary>
internal partial class HooksPageView : SettingsPageViewBase
{
    private HooksSettingsPage? _watched;
    private bool _syncing;

    /// <summary>Creates a view with no page.</summary>
    public HooksPageView()
    {
        InitializeComponent();
        RunHooksBox.IsCheckedChanged += OnRunHooksChanged;
        LogView.OnCopied(OnLogCopied);
        Start();
    }

    /// <summary>Gets the Run hooks switch.</summary>
    internal CheckBox RunHooks => RunHooksBox;

    /// <summary>Gets the block shown while Run hooks is off.</summary>
    internal Border Off => OffState;

    /// <summary>Gets the block shown for an untrusted folder.</summary>
    internal StackPanel Untrusted => UntrustedState;

    /// <summary>Gets the button of the untrusted-folder block.</summary>
    internal Button TrustFolder => TrustFolderButton;

    /// <summary>Gets the block shown when every hook file is invalid.</summary>
    internal StackPanel Invalid => InvalidState;

    /// <summary>Gets the block shown when there is no hook file.</summary>
    internal StackPanel Empty => EmptyState;

    /// <summary>Gets the button that copies the example file.</summary>
    internal Button CopyExample => CopyExampleButton;

    /// <summary>Gets the example file text.</summary>
    internal SelectableTextBlock Example => ExampleText;

    /// <summary>Gets the block that lists the hooks.</summary>
    internal StackPanel List => ListState;

    /// <summary>Gets the list of scope groups.</summary>
    internal ItemsControl Groups => GroupList;

    /// <summary>Gets the Trust all hooks in this folder button.</summary>
    internal Button TrustAll => TrustAllButton;

    /// <summary>Gets the card of the consent prompt.</summary>
    internal Border Consent => ConsentCard;

    /// <summary>Gets the list of quarantined sources.</summary>
    internal ItemsControl Quarantines => QuarantineList;

    /// <summary>Gets the list of diagnostics.</summary>
    internal ItemsControl Diagnostics => DiagnosticList;

    /// <summary>Gets the polite live region.</summary>
    internal TextBlock Announcement => AnnouncementText;

    /// <summary>Gets the assertive live region of a failed change.</summary>
    internal TextBlock ActionError => ActionErrorText;

    /// <summary>Gets the run log view.</summary>
    internal HookRunLogView Log => LogView;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    /// <inheritdoc />
    protected override void OnPageChanged(ISettingsPage? page)
    {
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnPagePropertyChanged;
            _watched.StopObserving();
        }

        _watched = page as HooksSettingsPage;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnPagePropertyChanged;
            _watched.StartObserving(static action => Dispatcher.UIThread.Post(action));
        }
    }

    /// <inheritdoc />
    protected override void OnRefresh(ISettingsPage page, PageStatus status) => Sync();

    private static HookPageRow? RowOf(object? sender) => (sender as Control)?.DataContext as HookPageRow;

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(HooksSettingsPage.ContentState), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(HooksSettingsPage.RunHooks), StringComparison.Ordinal))
        {
            Sync();
        }
    }

    // Shows exactly one state block for the main area and keeps the switch in step with the page.
    private void Sync()
    {
        if (_watched is not { } page)
        {
            return;
        }

        var state = page.ContentState;
        OffState.IsVisible = state == HooksContentState.RunHooksOff;
        UntrustedState.IsVisible = state == HooksContentState.UntrustedWorkspace;
        InvalidState.IsVisible = state == HooksContentState.InvalidConfig;
        EmptyState.IsVisible = state == HooksContentState.Empty;
        ListState.IsVisible = state == HooksContentState.Ready;
        _syncing = true;
        try
        {
            RunHooksBox.IsChecked = page.RunHooks;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnRunHooksChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing && _watched is { } page)
        {
            _ = page.SetRunHooksAsync(RunHooksBox.IsChecked == true, CancellationToken.None);
        }
    }

    private void OnTrustFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_watched is { } page)
        {
            _ = page.TrustWorkspaceAsync(CancellationToken.None);
        }
    }

    private void OnTrustAllClick(object? sender, RoutedEventArgs e)
    {
        if (_watched is { } page)
        {
            _ = page.ApproveAllAsync(CancellationToken.None);
        }
    }

    private void OnCopyExampleClick(object? sender, RoutedEventArgs e)
    {
        if (_watched is not { } page || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        _ = CopyExampleAsync(clipboard, page);
    }

    private static async Task CopyExampleAsync(Avalonia.Input.Platform.IClipboard clipboard, HooksSettingsPage page)
    {
        await clipboard.SetTextAsync(page.ExampleJson).ConfigureAwait(true);
        page.AnnounceExampleCopied();
    }

    private void OnReviewClick(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row)
        {
            _watched?.Select(row.Id);
        }
    }

    private void OnEnabledClick(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox box && RowOf(sender) is { } row && _watched is { } page)
        {
            _ = page.SetEnabledAsync(row.Id, box.IsChecked == true, CancellationToken.None);
        }
    }

    private void OnApproveClick(object? sender, RoutedEventArgs e)
    {
        if (_watched is { } page)
        {
            _ = page.ApproveSelectedAsync(CancellationToken.None);
        }
    }

    private void OnRevokeClick(object? sender, RoutedEventArgs e)
    {
        if (_watched is { } page)
        {
            _ = page.RevokeSelectedAsync(CancellationToken.None);
        }
    }

    private void OnCloseReviewClick(object? sender, RoutedEventArgs e) => _watched?.ClearSelection();

    private void OnLiftQuarantineClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || (button.DataContext as HookQuarantineRow) is not { } row || _watched is not { } page)
        {
            return;
        }

        var reason = (button.Parent as Panel)?.Children.OfType<TextBox>().FirstOrDefault()?.Text ?? string.Empty;
        _ = page.LiftQuarantineAsync(row.Source, reason, CancellationToken.None);
    }

    private void OnLogCopied() => _watched?.AnnounceLogCopied();
}
