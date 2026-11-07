using Autonogrammer.Presentation.Dispatch;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Dispatch;

/// <summary>
/// The approval prompt: the device, operation, capability, tool classes and the plain task text, with Approve once, Deny and Deny and pause
/// Dispatch. Each button raises its decision; the view decides nothing itself.
/// </summary>
internal partial class DispatchApprovalView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="DispatchApprovalView"/> class.</summary>
    public DispatchApprovalView()
    {
        InitializeComponent();
        ApprovalHeading.Text = DispatchCopy.ApprovalHeading;
        FromLabel.Text = DispatchCopy.ApprovalFromLabel;
        OperationLabel.Text = DispatchCopy.ApprovalOperationLabel;
        CapabilityLabel.Text = DispatchCopy.ApprovalCapabilityLabel;
        ToolsLabel.Text = DispatchCopy.ApprovalToolsLabel;
        ApproveOnceButton.Content = DispatchCopy.ApproveOnceLabel;
        DenyButton.Content = DispatchCopy.DenyLabel;
        DenyAndPauseButton.Content = DispatchCopy.DenyAndPauseLabel;

        ApproveOnceButton.Click += (_, _) => ApproveOnce?.Invoke(this, EventArgs.Empty);
        DenyButton.Click += (_, _) => Deny?.Invoke(this, EventArgs.Empty);
        DenyAndPauseButton.Click += (_, _) => DenyAndPause?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the user approves the command for this one run.</summary>
    public event EventHandler? ApproveOnce;

    /// <summary>Raised when the user refuses the command.</summary>
    public event EventHandler? Deny;

    /// <summary>Raised when the user refuses the command and pauses Dispatch on this device.</summary>
    public event EventHandler? DenyAndPause;

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ToolsText.Text = DataContext is DispatchApprovalModel approval ? string.Join(", ", approval.ToolClasses) : string.Empty;
    }
}
