using System.ComponentModel;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Settings;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Network page: the Run diagnostics button, a Cancel button while a run is in progress, a polite live region for the result, and the
/// list of endpoints with what each answered. A run starts only from the button; the view never starts one by itself.
/// </summary>
internal partial class NetworkPageView : SettingsPageViewBase
{
    private NetworkSettingsPage? _watched;

    /// <summary>Creates a view with no page.</summary>
    public NetworkPageView()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets the Run diagnostics button.</summary>
    internal Button Run => RunButton;

    /// <summary>Gets the Cancel button, shown while a run is in progress.</summary>
    internal Button CancelRun => CancelButton;

    /// <summary>Gets the polite live region.</summary>
    internal TextBlock Announcement => AnnouncementText;

    /// <summary>Gets the text shown before the first run.</summary>
    internal TextBlock NotRun => NotRunText;

    /// <summary>Gets the list of result lines.</summary>
    internal ItemsControl Results => CheckRows;

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
        }

        _watched = page as NetworkSettingsPage;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnPagePropertyChanged;
        }
    }

    private void OnRunClick(object? sender, RoutedEventArgs e)
    {
        if (_watched is { } page)
        {
            _ = page.RunDiagnosticsAsync(CancellationToken.None);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _watched?.CancelRun();

    // Focus follows the button that takes over: Cancel while a run is in progress, Run again when it ended with Cancel focused.
    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(NetworkSettingsPage.IsRunning), StringComparison.Ordinal) || _watched is not { } page)
        {
            return;
        }

        // A button that was hidden or disabled a moment ago may already have lost the focus, so "no focus" counts as "the button had it".
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (page.IsRunning)
        {
            if (focused is null || ReferenceEquals(focused, RunButton))
            {
                CancelButton.FocusWhenReady();
            }
        }
        else if (focused is null || ReferenceEquals(focused, CancelButton))
        {
            RunButton.FocusWhenReady();
        }
    }
}
