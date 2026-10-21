namespace OmpOS.Contracts.Errors;

/// <summary>
/// A typed OmpOS error: machine-readable code plus human-readable detail.
/// Carried as a value (not thrown) across the harness and API boundaries.
/// </summary>
public sealed record OmpOSError
{
    /// <summary>Gets the machine-readable error code.</summary>
    public required ErrorCode Code { get; init; }

    /// <summary>Gets the human-readable detail.</summary>
    public required string Detail { get; init; }
}
