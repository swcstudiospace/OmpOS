using Autonogrammer.Harness.Mcp;

namespace Autonogrammer.Native.Views.Mcp;

/// <summary>One tool offered to the model for the session, shown in the offered list.</summary>
/// <param name="Id">The offered tool.</param>
internal sealed record McpOfferedRow(McpToolId Id)
{
    /// <summary>Gets the server and tool name shown for the row.</summary>
    public string Label => Id.ToString();
}
