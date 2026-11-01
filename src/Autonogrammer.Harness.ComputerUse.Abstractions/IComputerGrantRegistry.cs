namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>Grant snapshot and revoke. Implemented by the grant store; Presentation talks only to this port.</summary>
public interface IComputerGrantRegistry
{
    /// <summary>Current grants.</summary>
    /// <returns>The snapshot.</returns>
    IReadOnlyList<ComputerGrantView> Snapshot();

    /// <summary>Revokes one grant.</summary>
    /// <param name="grantId">The grant id.</param>
    void Revoke(string grantId);
}

