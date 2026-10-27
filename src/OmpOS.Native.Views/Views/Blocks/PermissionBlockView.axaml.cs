using Avalonia.Controls;
using OmpOS.Contracts.Ui;

namespace OmpOS.Native.Views.Blocks;

/// <summary>
/// A permission request. The options are listed verbatim, in the order the agent gave them, and there are no decision
/// controls: the decision is made by policy, not by the viewer.
/// </summary>
internal partial class PermissionBlockView : UserControl
{
    /// <summary>Builds the read-only view for one permission request.</summary>
    /// <param name="block">The permission block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public PermissionBlockView(PermissionBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        Prompt.Text = block.Prompt;
        foreach (var option in block.Options)
        {
            OptionList.Children.Add(BlockControls.AsMono(BlockControls.Text(option, "permission-option")));
        }
    }
}
