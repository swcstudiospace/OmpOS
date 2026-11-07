namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>Kill-switch state the indicator and the gate share.</summary>
public enum KillSwitchState
{
    /// <summary>Not registered.</summary>
    Unregistered = 0,

    /// <summary>Registered and idle or acting.</summary>
    Armed = 1,

    /// <summary>Tripped. Every later authorisation is <see cref="DenialCode.Revoked"/> until the user re-arms.</summary>
    Tripped = 2,
}

