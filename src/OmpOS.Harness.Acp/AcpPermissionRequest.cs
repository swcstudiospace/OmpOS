namespace OmpOS.Harness.Acp;

/// <summary>A permission request the agent sent for a tool call, as seen by an <see cref="AcpApprovalPolicy"/>.</summary>
/// <param name="CallId">The tool call identifier.</param>
/// <param name="Name">The tool call title, or its kind when no title was sent.</param>
/// <param name="Kind">The tool kind, for example <c>read</c> or <c>execute</c>. Empty when the agent sent none.</param>
/// <param name="PayloadJson">The raw JSON params of the request.</param>
public sealed record AcpPermissionRequest(string CallId, string Name, string Kind, string PayloadJson);
