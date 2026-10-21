namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// A network rule.
/// </summary>
public sealed record NetworkRule
{
    /// <summary>Gets the destination CIDR or hostname.</summary>
    public required string Destination { get; init; }

    /// <summary>Gets the port or port range.</summary>
    public required string Port { get; init; }

    /// <summary>Gets the protocol.</summary>
    public required string Protocol { get; init; }

    /// <summary>Gets the action.</summary>
    public required NetworkAction Action { get; init; }
}
