namespace Autonogrammer.Presentation.Mcp;

/// <summary>One configuration problem as a row: the severity word and the sentence, with the file and server it belongs to.</summary>
public sealed class McpNoticeRow
{
    /// <summary>Creates a row.</summary>
    /// <param name="notice">The problem.</param>
    public McpNoticeRow(McpNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        Notice = notice;
        SeverityText = notice.Severity switch
        {
            McpNoticeSeverity.Error => "Error",
            McpNoticeSeverity.Warning => "Warning",
            _ => "Note",
        };
        Text = notice.ServerName is null
            ? notice.Origin + ": " + notice.Message
            : notice.Origin + ", server " + notice.ServerName + ": " + notice.Message;
        Name = SeverityText + ". " + Text;
    }

    /// <summary>Gets the problem.</summary>
    public McpNotice Notice { get; }

    /// <summary>Gets the severity word.</summary>
    public string SeverityText { get; }

    /// <summary>Gets the sentence with its file and server.</summary>
    public string Text { get; }

    /// <summary>Gets the accessible name: the severity word and the sentence.</summary>
    public string Name { get; }
}
