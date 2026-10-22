using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Contracts.Ui;

namespace OmpOS.Native.Views.Blocks;

/// <summary>A tool-call card: the tool's name and its status. The arguments are not drawn.</summary>
internal partial class ToolCallBlockView : UserControl
{
    /// <summary>Builds the card for one tool call.</summary>
    /// <param name="block">The tool-call block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public ToolCallBlockView(ToolCallBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        ToolName.Text = block.ToolName;
        ToolStatus.Text = block.Status;
        AutomationProperties.SetName(this, $"Tool call {block.ToolName}, {block.Status}");
    }
}
