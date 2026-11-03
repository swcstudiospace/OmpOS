using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The policy answer for one command.</summary>
/// <param name="Allowed">True when the command may proceed to its handler or to the local approval.</param>
/// <param name="Reason">The closed refusal reason when not allowed; null otherwise.</param>
/// <param name="Task">The parsed task fields for a code or cowork command; null for session operations.</param>
public readonly record struct DispatchPolicyDecision(bool Allowed, DispatchFailureReason? Reason, DispatchTaskPayload? Task)
{
    /// <summary>Builds an allowed decision.</summary>
    /// <param name="task">The parsed task, or null.</param>
    /// <returns>The decision.</returns>
    public static DispatchPolicyDecision Allow(DispatchTaskPayload? task = null) => new(true, null, task);

    /// <summary>Builds a refused decision.</summary>
    /// <param name="reason">The closed reason.</param>
    /// <returns>The decision.</returns>
    public static DispatchPolicyDecision Refuse(DispatchFailureReason reason) => new(false, reason, null);
}
