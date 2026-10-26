using Avalonia.Controls;
using OmpOS.Contracts.Ui;

namespace OmpOS.Native.Views.Blocks;

/// <summary>A user turn: the prompt text verbatim, as plain text, ahead of the assistant output of the same turn.</summary>
internal partial class UserMessageView : UserControl
{
    /// <summary>Builds the view for one user turn.</summary>
    /// <param name="block">The user message block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public UserMessageView(UserMessageBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        Body.Text = block.Text;
    }
}
