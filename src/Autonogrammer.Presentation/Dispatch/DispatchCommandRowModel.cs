using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Presentation.Dispatch;

/// <summary>One command in a list: its phase, the text that says it, and its task text as plain, capped text (D-29).</summary>
/// <param name="CommandId">The command.</param>
/// <param name="Operation">The operation.</param>
/// <param name="Phase">The phase.</param>
/// <param name="StatusText">The text for the phase, or for the failure reason when the command failed.</param>
/// <param name="TaskText">The task text, plain and capped.</param>
public sealed record DispatchCommandRowModel(string CommandId, string Operation, DispatchPhase Phase, string StatusText, string TaskText)
{
    /// <summary>Builds the row for a command.</summary>
    /// <param name="commandId">The command.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="phase">The phase.</param>
    /// <param name="taskText">The task text, which is capped.</param>
    /// <param name="failure">The failure reason, when the command failed.</param>
    /// <returns>The row.</returns>
    public static DispatchCommandRowModel From(
        string commandId,
        string operation,
        DispatchPhase phase,
        string taskText,
        DispatchFailureReason? failure = null)
    {
        ArgumentNullException.ThrowIfNull(commandId);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(taskText);

        var status = failure is { } reason ? DispatchCopy.ForFailure(reason) : DispatchCopy.ForPhase(phase);
        return new DispatchCommandRowModel(commandId, operation, phase, status, DispatchText.Plain(taskText));
    }
}
