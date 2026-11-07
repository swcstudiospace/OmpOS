namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// What this desktop's Dispatch settings and approval prompt need from the head: its pairing, its per-capability policy and the approvals
/// waiting for the local user. The web head passes no implementation; its pair section says to pair in the desktop app.
/// </summary>
public interface IThisDeviceHost
{
    /// <summary>Reads this desktop's pairing, capability policy and pending approvals.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The state.</returns>
    /// <exception cref="DispatchHostException">The call failed.</exception>
    Task<DispatchThisDevice> GetThisDeviceAsync(CancellationToken cancellationToken);

    /// <summary>Allows or refuses one capability on this desktop.</summary>
    /// <param name="capability">The capability.</param>
    /// <param name="enabled">Whether the capability is allowed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the policy is stored.</returns>
    /// <exception cref="DispatchHostException">The call failed.</exception>
    Task SetCapabilityAsync(string capability, bool enabled, CancellationToken cancellationToken);

    /// <summary>Approves or denies a command waiting for the local user.</summary>
    /// <param name="approvalId">The approval.</param>
    /// <param name="approved">Whether the user approved it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the decision is recorded.</returns>
    /// <exception cref="DispatchHostException">The call failed.</exception>
    Task ResolveApprovalAsync(string approvalId, bool approved, CancellationToken cancellationToken);
}
