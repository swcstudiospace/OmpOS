namespace Autonogrammer.Harness.Abstractions;

/// <summary>The engine requests permission for a tool call.</summary>
/// <param name="CallId">The tool call identifier.</param>
/// <param name="Name">The tool name.</param>
/// <param name="PayloadJson">The raw JSON payload of the request.</param>
/// <param name="RequestId">
/// The identifier a decision is addressed to, unique within its session. <see langword="null"/> when the producer
/// cannot receive a decision, so the request is answered by policy alone.
/// </param>
public sealed record PermissionRequestEvent(string CallId, string Name, string PayloadJson, string? RequestId = null) : HarnessEvent;
