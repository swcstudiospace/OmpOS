using Autonogrammer.Presentation.Approvals;

namespace Autonogrammer.Native.Engine;

/// <summary>
/// The production default approval bridge while no approval service is connected in this build. It never allows a tool:
/// every decision reports <see cref="ToolApprovalOutcome.Failed"/>, so the request stays pending.
/// </summary>
public sealed class UnconnectedToolApprovalBridge : IToolApprovalBridge, IToolApprovalBridgeAvailability
{
    /// <summary>The reason shown to the user while no approval service is connected.</summary>
    public const string UnavailableText = "No approval service is connected in this build.";

    /// <inheritdoc/>
    public string? UnavailableMessage => UnavailableText;

    /// <inheritdoc/>
    public Task<ToolApprovalOutcome> DecideAsync(string sessionId, string requestId, ToolApprovalDecision decision, CancellationToken cancellationToken) =>
        Task.FromResult(ToolApprovalOutcome.Failed);
}
