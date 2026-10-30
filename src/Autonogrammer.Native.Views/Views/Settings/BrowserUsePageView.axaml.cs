using Autonogrammer.Presentation.Settings.BrowserUse;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>The Browser page: Chromium state, policy editors and the acquisition dialog. A download starts only from Confirm.</summary>
internal partial class BrowserUsePageView : SettingsPageViewBase
{
    /// <summary>Creates a view with no page.</summary>
    public BrowserUsePageView()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets the Chromium state sentence.</summary>
    internal TextBlock StateLine => StateSentence;

    /// <summary>Gets the state action.</summary>
    internal Button Fix => FixButton;

    /// <summary>Gets the install action, hidden unless a manifest row exists.</summary>
    internal Button Acquire => AcquireButton;

    /// <summary>Gets the acquisition dialog text.</summary>
    internal TextBlock AcquisitionText => DialogText;

    /// <summary>Gets the confirm button.</summary>
    internal Button Confirm => ConfirmButton;

    /// <summary>Gets the cancel button of the acquisition dialog.</summary>
    internal Button Cancel => CancelButton;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    private BrowserUseSettingsPage? PageModel => DataContext as BrowserUseSettingsPage;

    private void OnFixClick(object? sender, RoutedEventArgs e) => PageModel?.OpenAcquisition();

    private void OnAcquireClick(object? sender, RoutedEventArgs e) => PageModel?.OpenAcquisition();

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (PageModel?.Dialog is { } dialog)
        {
            _ = dialog.ConfirmAsync(CancellationToken.None);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => PageModel?.DismissAcquisition();
}
