using Autonogrammer.Contracts.Ui;
using Avalonia.Automation;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>A tool result: the tool's name, the status it reported, and its output as monospace text.</summary>
internal partial class ToolResultBlockView : UserControl
{
    /// <summary>Builds the view for one tool result.</summary>
    /// <param name="block">The tool-result block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public ToolResultBlockView(ToolResultBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        ResultTitle.Text = $"Result: {block.ToolName}";
        ResultStatus.Text = block.Status;
        OutputText.Text = block.Output;
        OutputText.FontFamily = BlockTokens.Monospace;
        AutomationProperties.SetName(this, $"Tool result {block.ToolName}, {block.Status}");
    }
}
