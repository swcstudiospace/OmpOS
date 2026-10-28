using Avalonia.Automation;
using Avalonia.Controls;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>A tool-call row: a status glyph, the tool's name and its status in words. The arguments are not drawn. Hook notices of the call hang under the row.</summary>
internal partial class ToolCallBlockView : UserControl
{
    /// <summary>Builds the row for one tool call.</summary>
    /// <param name="block">The tool-call block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public ToolCallBlockView(ToolCallBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        CallId = block.CallId;
        ToolName.Text = block.ToolName;
        ToolStatus.Text = block.Status;
        StatusGlyph.IconKey = GlyphOf(block.Status);
        AutomationProperties.SetName(this, $"Tool call {block.ToolName}, {block.Status}");
    }

    /// <summary>Gets the id of the call the row shows.</summary>
    internal string CallId { get; }

    /// <summary>Gets the panel that holds the hook notices under the row.</summary>
    internal Panel Notices => NoticeHost;

    /// <summary>Shows a notice under the row. A control that sits under another parent (an older row of the same call) is moved here.</summary>
    /// <param name="notice">The notice control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notice"/> is null.</exception>
    internal void AttachNotice(Control notice)
    {
        ArgumentNullException.ThrowIfNull(notice);

        if (notice.Parent is Panel parent)
        {
            if (ReferenceEquals(parent, NoticeHost))
            {
                return;
            }

            parent.Children.Remove(notice);
        }

        NoticeHost.Children.Add(notice);
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
