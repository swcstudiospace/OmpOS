namespace OmpOS.Harness.Abstractions;

/// <summary>A tool invocation has started.</summary>
/// <param name="CallId">The tool call identifier.</param>
/// <param name="Name">The tool name.</param>
/// <param name="PayloadJson">The raw JSON payload of the call.</param>
public sealed record ToolCallEvent(string CallId, string Name, string PayloadJson) : HarnessEvent;
