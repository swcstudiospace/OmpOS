using System.Diagnostics;
using Autonogrammer.Presentation.Settings.BrowserUse;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>The Browser page: Chromium state, policy editors and the acquisition dialog. A download starts only from Confirm.</summary>
internal partial class BrowserUsePageView : SettingsPageViewBase
{
    private BrowserUseAcquisitionDialog? _confirming;

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

    /// <summary>Gets the line that says why the last acquisition was refused. It is empty and hidden when there is no refusal.</summary>
    internal TextBlock AcquisitionErrorLine => AcquisitionError;

    /// <summary>Gets the confirm button.</summary>
    internal Button Confirm => ConfirmButton;

    /// <summary>Gets the cancel button of the acquisition dialog.</summary>
    internal Button Cancel => CancelButton;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    private BrowserUseSettingsPage? PageModel => DataContext as BrowserUseSettingsPage;

    private void OnFixClick(object? sender, RoutedEventArgs e) => OpenDialog();

    private void OnAcquireClick(object? sender, RoutedEventArgs e) => OpenDialog();

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (_confirming is null && PageModel?.Dialog is { } dialog)
        {
            _ = ConfirmAsync(dialog);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        ShowError(string.Empty);
        PageModel?.DismissAcquisition();
    }

    private void OpenDialog()
    {
        ShowError(string.Empty);
        PageModel?.OpenAcquisition();
    }

    // Confirm is off while the download runs, so a second confirm cannot start one. A refusal is shown under the dialog text, and an
    // unexpected fault is traced and shown as a failed start. Both are read on the context that owns the page.
    private async Task ConfirmAsync(BrowserUseAcquisitionDialog dialog)
    {
        _confirming = dialog;
        ConfirmButton.IsEnabled = false;
        ShowError(string.Empty);
        try
        {
            await dialog.ConfirmAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            Trace.TraceError(BrowserUseCopy.AcquisitionFaultTrace + exception.Message);
            ShowError(BrowserUseCopy.AcquisitionFailed);
        }
        finally
        {
            _confirming = null;
            ConfirmButton.IsEnabled = true;
            if (ReferenceEquals(PageModel?.Dialog, dialog) && dialog.ErrorText is { } refusal)
            {
                ShowError(refusal);
            }
        }
    }

    private void ShowError(string text)
    {
        AcquisitionError.Text = text;
        AcquisitionError.IsVisible = text.Length > 0;
    }
}
