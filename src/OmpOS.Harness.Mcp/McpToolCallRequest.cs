namespace OmpOS.Harness.Mcp;

/// <summary>A tool call awaiting an approval decision.</summary>
/// <param name="SessionId">The session the model is calling from.</param>
/// <param name="Tool">The tool being called.</param>
/// <param name="Arguments">The arguments the model supplied.</param>
public sealed record McpToolCallRequest(
    string SessionId,
    McpToolId Tool,
    IReadOnlyDictionary<string, object?> Arguments);
