namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>Redacted audit row for the settings page. Typed text and clipboard contents are absent.</summary>
public sealed record AuditRecordView
{
    /// <summary>Sequence number.</summary>
    public required long Seq { get; init; }

    /// <summary>UTC time recorded in the log.</summary>
    public required DateTimeOffset Time { get; init; }

    /// <summary>Session id.</summary>
    public required string SessionId { get; init; }

    /// <summary>Tool name.</summary>
    public required string Tool { get; init; }

    /// <summary>Host-built action summary.</summary>
    public required string Summary { get; init; }

    /// <summary>Application identity.</summary>
    public required string AppIdentity { get; init; }

    /// <summary>Title truncated to 80 characters.</summary>
    public required string TitleTruncated { get; init; }

    /// <summary>Decision word, allowed or denied.</summary>
    public required string Decision { get; init; }

    /// <summary>Denial code when the action was denied.</summary>
    public DenialCode? Denial { get; init; }
}

