namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Network policy for a sandbox.
/// </summary>
public sealed record NetworkPolicy
{
    /// <summary>
    /// Gets the default network policy (deny all outbound).
    /// </summary>
    public static NetworkPolicy DefaultDeny => new() { OutboundDefault = OutboundDefault.Deny, Rules = [] };

    /// <summary>
    /// Gets the outbound default action.
    /// </summary>
    public required OutboundDefault OutboundDefault { get; init; }

    /// <summary>
    /// Gets the explicit network rules.
    /// </summary>
    public required IReadOnlyList<NetworkRule> Rules { get; init; }
}
