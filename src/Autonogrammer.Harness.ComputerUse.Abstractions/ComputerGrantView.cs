namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>A grant as the UI and the gate read it. Dispatch-origin grants last at most 30 minutes and never auto-approve typing.</summary>
public sealed record ComputerGrantView
{
    /// <summary>Grant id.</summary>
    public required string GrantId { get; init; }

    /// <summary>Session id.</summary>
    public required string SessionId { get; init; }

    /// <summary>Application identity.</summary>
    public required string AppIdentity { get; init; }

    /// <summary>Tier.</summary>
    public required ComputerGrantTier Tier { get; init; }

    /// <summary>Local or Dispatch.</summary>
    public required ComputerActionOrigin Origin { get; init; }

    /// <summary>Expiry supplied by the store. This record does not read the clock.</summary>
    public required DateTimeOffset Expiry { get; init; }

    /// <summary>Whether typing in this application is auto-approved for the session. Credential-titled windows still ask.</summary>
    public required bool AutoApproveTyping { get; init; }
}

