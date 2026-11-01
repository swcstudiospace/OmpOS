namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Hub to device revocation.</summary>
public sealed record DispatchRevokeFrame
{
    /// <summary>Protocol version. Always 1.</summary>
    public required int V { get; init; }

    /// <summary>Frame type <c>revoke</c>.</summary>
    public required string T { get; init; }

    /// <summary>Optional revoke reason wire name.</summary>
    public string? Reason { get; init; }
}
