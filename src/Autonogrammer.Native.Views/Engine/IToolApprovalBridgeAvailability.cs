using Autonogrammer.Presentation.Approvals;

namespace Autonogrammer.Native.Engine;

/// <summary>
/// Implemented by an approval bridge that can say why it cannot record decisions. <see cref="IToolApprovalBridge"/> reports
/// only an outcome, so the run controls read this detail to show the user why a decision was not recorded.
/// </summary>
internal interface IToolApprovalBridgeAvailability
{
    /// <summary>Gets the user-visible reason the bridge cannot record decisions, or null when it is connected.</summary>
    string? UnavailableMessage { get; }
}
