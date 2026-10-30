using Autonogrammer.Presentation.Computer;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Computer;

/// <summary>Approval dialog. Deny takes focus when the window opens. No timer allows.</summary>
public partial class ComputerApprovalDialog : Window
{
    /// <summary>Creates a dialog over the model.</summary>
    /// <param name="model">The approval model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public ComputerApprovalDialog(ComputerApprovalViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        InitializeComponent();
        DataContext = model;
    }

    /// <summary>Gets the Deny button. It is the default focus.</summary>
    public Button Deny => DenyButton;

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        DenyButton.Focus();
    }
}
