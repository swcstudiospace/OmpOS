namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>Kill-switch control the indicator and settings page use.</summary>
public interface IKillSwitchControl
{
    /// <summary>Current state.</summary>
    KillSwitchState State { get; }

    /// <summary>Raised when <see cref="State"/> changes.</summary>
    event EventHandler<KillSwitchStateChangedEventArgs>? StateChanged;

    /// <summary>Trips the switch: release input, revoke, cancel the turn.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>A completed task.</returns>
    ValueTask TripAsync(CancellationToken cancellationToken);

    /// <summary>Re-arms after the user asks.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>A completed task.</returns>
    ValueTask RearmAsync(CancellationToken cancellationToken);
}

