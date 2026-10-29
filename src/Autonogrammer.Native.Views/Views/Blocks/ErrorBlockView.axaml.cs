using Autonogrammer.Contracts.Ui;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>An error block: its message. The block offers no action, so retry is not drawn here.</summary>
internal partial class ErrorBlockView : UserControl
{
    /// <summary>Builds the view for one error.</summary>
    /// <param name="block">The error block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public ErrorBlockView(ErrorBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        Message.Text = block.Message;
    }
}
