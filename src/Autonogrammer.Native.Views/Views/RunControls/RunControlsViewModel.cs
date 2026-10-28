using System.Collections.ObjectModel;
using Autonogrammer.Native.Engine;
using Autonogrammer.Presentation.Approvals;

namespace Autonogrammer.Native.Views.RunControls;

/// <summary>
/// The pending tool-approval requests of the main window and the coordinator that sends each decision (ADE-02). A request
/// leaves <see cref="ToolApprovalStatus.Pending"/> only when the user decides it and the bridge answers; nothing is
/// allowed by default.
/// </summary>
internal sealed class RunControlsViewModel
{
    /// <summary>Creates the run controls that send decisions through <paramref name="bridge"/>.</summary>
    /// <param name="bridge">The approval bridge that records each decision.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bridge"/> is null.</exception>
    public RunControlsViewModel(IToolApprovalBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(bridge);

        Coordinator = new ToolApprovalCoordinator(bridge);
        UnavailableDetail = (bridge as IToolApprovalBridgeAvailability)?.UnavailableMessage;
    }

    /// <summary>Gets the coordinator that sends each user decision to the bridge.</summary>
    public ToolApprovalCoordinator Coordinator { get; }

    /// <summary>Gets the pending requests, in the order the run raised them. Each row shows one request.</summary>
    public ObservableCollection<PendingToolApproval> Approvals { get; } = [];

    /// <summary>Gets the reason the bridge cannot record decisions, or null when it is connected.</summary>
    public string? UnavailableDetail { get; }
}
