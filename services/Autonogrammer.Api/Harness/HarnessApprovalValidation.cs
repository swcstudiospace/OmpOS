using Autonogrammer.Api.Options;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// Checks the <c>Harness:Acp:Approval</c> keys of <see cref="HarnessOptions"/>. <see cref="EngineIsolationOptionsValidator"/> runs it when the options are
/// validated on start. Messages name keys and the offending kind; they never echo a tool name or any other value.
/// </summary>
internal static class HarnessApprovalValidation
{
    /// <summary>Adds one failure for every invalid entry of <paramref name="approval"/> to <paramref name="failures"/>.</summary>
    /// <param name="approval">The approval options.</param>
    /// <param name="failures">Receives the failure messages.</param>
    public static void Collect(HarnessApprovalOptions approval, List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(failures);

        var seenKinds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < approval.Kinds.Count; index++)
        {
            var kind = approval.Kinds[index];
            if (string.IsNullOrWhiteSpace(kind))
            {
                failures.Add($"Harness:Acp:Approval:Kinds:{index} must not be blank.");
            }
            else if (!HarnessApprovalOptions.AcpToolKinds.Contains(kind, StringComparer.Ordinal))
            {
                failures.Add($"Harness:Acp:Approval:Kinds:{index} '{kind}' is not an ACP tool kind; use one of {string.Join(", ", HarnessApprovalOptions.AcpToolKinds)}.");
            }
            else if (!seenKinds.Add(kind))
            {
                failures.Add($"Harness:Acp:Approval:Kinds:{index} '{kind}' is listed more than once.");
            }
        }

        var seenTools = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < approval.GrantableTools.Count; index++)
        {
            var tool = approval.GrantableTools[index];
            if (string.IsNullOrWhiteSpace(tool))
            {
                failures.Add($"Harness:Acp:Approval:GrantableTools:{index} must not be blank.");
            }
            else if (!seenTools.Add(tool))
            {
                failures.Add($"Harness:Acp:Approval:GrantableTools:{index} is listed more than once.");
            }
        }

        if (approval.DecisionTimeoutSeconds is < 1 or > HarnessApprovalOptions.MaxDecisionTimeoutSeconds)
        {
            failures.Add($"Harness:Acp:Approval:DecisionTimeoutSeconds must be between 1 and {HarnessApprovalOptions.MaxDecisionTimeoutSeconds}.");
        }
    }
}
