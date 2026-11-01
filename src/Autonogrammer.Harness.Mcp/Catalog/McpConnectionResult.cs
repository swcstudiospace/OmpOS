namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>What a connection check found.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Reason">One sentence for a failure. The catalog removes every environment and header value from it and cuts it short, but a checker should not include one.</param>
/// <param name="ToolCount">How many tools the server listed, when it connected.</param>
/// <param name="Tools">The tools the server listed, or null when the check did not list any. Names, descriptions and input schemas only.</param>
public sealed record McpConnectionResult(
    McpConnectionOutcome Outcome,
    string? Reason = null,
    int ToolCount = 0,
    IReadOnlyList<McpToolDescriptor>? Tools = null)
{
    /// <summary>Prints the outcome, the fixed reason and the tool names. Schemas, arguments and URLs are omitted.</summary>
    /// <returns>A one-line description without a secret.</returns>
    public override string ToString()
    {
        var names = Tools is null ? string.Empty : string.Join(',', Tools.Select(tool => tool.Id.ToolName));
        return $"McpConnectionResult {{ Outcome = {Outcome}, Reason = {Reason}, ToolCount = {ToolCount}, ToolNames = {names} }}";
    }
}
