using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// The desktop-side rules for a verified command. Nothing here can be raised by a controller: the policy and the configuration are the
/// desktop's. A Computer Use task needs all three of: the configuration keys, the desktop opt-in, and (at run time) the local approval.
/// </summary>
public static class DispatchPolicyEvaluator
{
    /// <summary>Evaluates one command against the policy in force.</summary>
    /// <param name="command">The verified command.</param>
    /// <param name="policy">The desktop policy.</param>
    /// <param name="computerUse">The Computer Use configuration keys.</param>
    /// <returns>The decision.</returns>
    public static DispatchPolicyDecision Evaluate(DispatchCommand command, DispatchPolicy policy, DispatchComputerUseConfig computerUse)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(policy);
        if (ExpectedCapability(command.Op) != command.Capability)
        {
            return DispatchPolicyDecision.Refuse(DispatchFailureReason.PayloadInvalid);
        }

        return command.Op switch
        {
            DispatchOp.SessionList or DispatchOp.SessionCancel or DispatchOp.CommandCancel => DispatchPolicyDecision.Allow(),
            DispatchOp.SessionSteer => policy.Paused ? DispatchPolicyDecision.Refuse(DispatchFailureReason.PolicyDenied) : DispatchPolicyDecision.Allow(),

            // A decision of kind computer is never accepted from Dispatch. Other kinds have no Dispatch route yet, so every
            // approval.decide is refused rather than guessed at.
            DispatchOp.ApprovalDecide => DispatchPolicyDecision.Refuse(DispatchFailureReason.PolicyDenied),
            DispatchOp.CodePrompt => EvaluateCode(command, policy),
            DispatchOp.CoworkTask => EvaluateCowork(command, policy, computerUse),
            _ => DispatchPolicyDecision.Refuse(DispatchFailureReason.UnsupportedOperation),
        };
    }

    /// <summary>Returns the capability an operation belongs to.</summary>
    /// <param name="op">The operation.</param>
    /// <returns>The capability.</returns>
    public static DispatchCapability ExpectedCapability(DispatchOp op) => op switch
    {
        DispatchOp.CodePrompt => DispatchCapability.Code,
        DispatchOp.CoworkTask => DispatchCapability.Cowork,
        _ => DispatchCapability.Sessions,
    };

    private static DispatchPolicyDecision EvaluateCode(DispatchCommand command, DispatchPolicy policy)
    {
        if (policy.Paused)
        {
            return DispatchPolicyDecision.Refuse(DispatchFailureReason.PolicyDenied);
        }

        if (!policy.CodeEnabled)
        {
            return DispatchPolicyDecision.Refuse(DispatchFailureReason.CapabilityDisabled);
        }

        if (!DispatchTaskPayload.TryParse(command.Payload, out var task))
        {
            return DispatchPolicyDecision.Refuse(DispatchFailureReason.PayloadInvalid);
        }

        // Only cowork.task may name tool classes. A code session never registers a computer tool.
        return task.ToolClasses.Count == 0
            ? DispatchPolicyDecision.Allow(task)
            : DispatchPolicyDecision.Refuse(DispatchFailureReason.PayloadInvalid);
    }

    private static DispatchPolicyDecision EvaluateCowork(DispatchCommand command, DispatchPolicy policy, DispatchComputerUseConfig computerUse)
    {
        if (policy.Paused)
        {
            return DispatchPolicyDecision.Refuse(DispatchFailureReason.PolicyDenied);
        }

        if (!policy.CoworkEnabled)
        {
            return DispatchPolicyDecision.Refuse(DispatchFailureReason.CapabilityDisabled);
        }

        if (!DispatchTaskPayload.TryParse(command.Payload, out var task))
        {
            return DispatchPolicyDecision.Refuse(DispatchFailureReason.PayloadInvalid);
        }

        foreach (var toolClass in task.ToolClasses)
        {
            switch (toolClass)
            {
                case DispatchTaskPayload.BrowserUse when !policy.BrowserUseEnabled:
                    return DispatchPolicyDecision.Refuse(DispatchFailureReason.CapabilityDisabled);
                case DispatchTaskPayload.ComputerUse when !(computerUse.ComputerUseEnabled && computerUse.AllowDispatchSessions && policy.ComputerUseOptIn):
                    return DispatchPolicyDecision.Refuse(DispatchFailureReason.CapabilityDisabled);
                case DispatchTaskPayload.BrowserUse:
                case DispatchTaskPayload.ComputerUse:
                    break;
                default:
                    return DispatchPolicyDecision.Refuse(DispatchFailureReason.PayloadInvalid);
            }
        }

        return DispatchPolicyDecision.Allow(task);
    }
}
