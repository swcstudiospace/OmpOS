namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>One denylist row. Built-in rows are read-only.</summary>
public sealed record ComputerDenylistEntry
{
    /// <summary>Application identity or catalogue key.</summary>
    public required string AppIdentity { get; init; }

    /// <summary><see langword="true"/> when the row is built-in and cannot be removed.</summary>
    public required bool BuiltIn { get; init; }
}

