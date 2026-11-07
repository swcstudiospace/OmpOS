namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// The approval prompt: the controller's device name, the operation, the capability, the tool classes and the first two hundred characters of
/// the task text, all as plain text (D-29).
/// </summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="DeviceName">The controller's device name.</param>
/// <param name="Operation">The operation.</param>
/// <param name="Capability">The capability.</param>
/// <param name="ToolClasses">The tool classes the command uses.</param>
/// <param name="TaskText">The task text, plain and capped.</param>
public sealed record DispatchApprovalModel(
    string ApprovalId,
    string DeviceName,
    string Operation,
    string Capability,
    IReadOnlyList<string> ToolClasses,
    string TaskText)
{
    /// <summary>Builds the prompt for a waiting approval.</summary>
    /// <param name="approvalId">The approval.</param>
    /// <param name="deviceName">The controller's device name.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="toolClasses">The tool classes.</param>
    /// <param name="taskText">The task text, which is capped.</param>
    /// <returns>The prompt.</returns>
    public static DispatchApprovalModel From(
        string approvalId,
        string deviceName,
        string operation,
        string capability,
        IReadOnlyList<string> toolClasses,
        string taskText)
    {
        ArgumentNullException.ThrowIfNull(approvalId);
        ArgumentNullException.ThrowIfNull(deviceName);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(toolClasses);
        ArgumentNullException.ThrowIfNull(taskText);

        return new DispatchApprovalModel(approvalId, deviceName, operation, capability, [.. toolClasses], DispatchText.Plain(taskText));
    }
}
