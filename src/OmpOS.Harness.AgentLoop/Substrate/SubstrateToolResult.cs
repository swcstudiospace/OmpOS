namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>The answer to one MCP <c>tools/call</c>: the first text block of the reply and the tool-error flag.</summary>
public sealed record SubstrateToolResult
{
    /// <summary>Gets the text of the reply. Substrate tools answer with JSON text, or with a plain sentence when there is nothing to return.</summary>
    public required string Text { get; init; }

    /// <summary>Gets a value indicating whether the tool reported an error. The text then describes a refusal and is not data.</summary>
    public required bool IsError { get; init; }
}
