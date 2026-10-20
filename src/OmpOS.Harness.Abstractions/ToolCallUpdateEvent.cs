namespace OmpOS.Harness.Abstractions;

/// <summary>A tool invocation has progressed or finished.</summary>
/// <param name="CallId">The tool call identifier.</param>
/// <param name="Status">The reported status.</param>
/// <param name="PayloadJson">The raw JSON payload of the update.</param>
public sealed record ToolCallUpdateEvent(string CallId, string Status, string PayloadJson) : HarnessEvent;
