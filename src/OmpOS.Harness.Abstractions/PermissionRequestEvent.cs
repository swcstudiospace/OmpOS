namespace OmpOS.Harness.Abstractions;

/// <summary>The engine requests permission for a tool call.</summary>
/// <param name="CallId">The tool call identifier.</param>
/// <param name="Name">The tool name.</param>
/// <param name="PayloadJson">The raw JSON payload of the request.</param>
public sealed record PermissionRequestEvent(string CallId, string Name, string PayloadJson) : HarnessEvent;
