using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Contracts.Ui;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Native.Views.Blocks;

/// <summary>A tool-call row: a status glyph, the tool's name and its status in words. The arguments are not drawn.</summary>
internal partial class ToolCallBlockView : UserControl
{
    /// <summary>Builds the row for one tool call.</summary>
    /// <param name="block">The tool-call block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public ToolCallBlockView(ToolCallBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        ToolName.Text = block.ToolName;
        ToolStatus.Text = block.Status;
        StatusGlyph.IconKey = GlyphOf(block.Status);
        AutomationProperties.SetName(this, $"Tool call {block.ToolName}, {block.Status}");
    }

    /// <summary>Chooses the glyph of the glyph table that matches a reported call status. The words next to it say the same thing.</summary>
    /// <param name="status">The status the call reported, such as <c>running</c>, <c>completed</c> or <c>failed</c>.</param>
    /// <returns>The glyph key: the pulsing ring while running, a check when completed, a cross when failed, a clock when pending, otherwise a ring.</returns>
    internal static string GlyphOf(string status) => status.ToUpperInvariant() switch
    {
        "RUNNING" or "IN_PROGRESS" => StatusIcon.Running,
        "COMPLETED" or "SUCCEEDED" => StatusIcon.Check,
        "FAILED" or "ERROR" => StatusIcon.Cross,
        "PENDING" or "QUEUED" => StatusIcon.Clock,
        _ => StatusIcon.Ring,
    };
}
