namespace Autonogrammer.Dispatch.Desktop;

/// <summary>What the local approval dialog shows. Every field is host-built or taken from the verified command; the dialog shows them as plain text.</summary>
/// <param name="DeviceName">The controller device name.</param>
/// <param name="ToolClasses">The tool classes the task names.</param>
/// <param name="TaskText">The first 200 characters of the task text.</param>
public sealed record DispatchApprovalRequest(string DeviceName, IReadOnlyList<string> ToolClasses, string TaskText);
