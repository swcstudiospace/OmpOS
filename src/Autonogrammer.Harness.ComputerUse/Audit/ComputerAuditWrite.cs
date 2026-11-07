using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Audit;

/// <summary>Host facts for one audit line. Typed text is hashed and dropped. Clipboard bytes are not a field.</summary>
public sealed record ComputerAuditWrite
{
    /// <summary>Session id.</summary>
    public required string SessionId { get; init; }

    /// <summary>Tool name.</summary>
    public required string Tool { get; init; }

    /// <summary>Host summary. Must not contain typed text or clipboard bytes.</summary>
    public required string Summary { get; init; }

    /// <summary>Application identity.</summary>
    public required string AppIdentity { get; init; }

    /// <summary>Window title. Stored truncated to 80 characters.</summary>
    public required string Title { get; init; }

    /// <summary>Decision word.</summary>
    public required string Decision { get; init; }

    /// <summary>Denial, or null.</summary>
    public DenialCode? Denial { get; init; }

    /// <summary>Approval origin.</summary>
    public required string ApprovalOrigin { get; init; }

    /// <summary>Outcome word, such as before or after.</summary>
    public required string Outcome { get; init; }

    /// <summary>Frame SHA-256, or null.</summary>
    public string? FrameSha256 { get; init; }

    /// <summary>Typed text length when <c>Audit:TypedText</c> is Off. The characters are not stored.</summary>
    public int? TypedLength { get; init; }

    /// <summary>SHA-256 of typed text, lowercase hex, or null.</summary>
    public string? TypedSha256 { get; init; }
}
