using Autonogrammer.Contracts.Ui;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>A diff placeholder: the changed path and why the diff itself is not drawn.</summary>
internal partial class DiffPlaceholderView : UserControl
{
    /// <summary>Builds the placeholder for one diff.</summary>
    /// <param name="block">The diff placeholder block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public DiffPlaceholderView(DiffPlaceholderBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        PathText.Text = block.Path;
        ReasonText.Text = block.Reason;
    }
}
