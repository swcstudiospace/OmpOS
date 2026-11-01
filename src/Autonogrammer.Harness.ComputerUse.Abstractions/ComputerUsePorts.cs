namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>
/// L1 ports that keep policy types and the backend out of Presentation, and keep web sign-in types out of the gate.
/// The Native head implements <see cref="IComputerUseAccessFacts"/> over the Cowork account gate.
/// </summary>
public static class ComputerUsePorts
{
}
