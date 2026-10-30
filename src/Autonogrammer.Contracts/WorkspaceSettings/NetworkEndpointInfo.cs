namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>One upstream the API is configured to talk to.</summary>
public sealed record NetworkEndpointInfo
{
    /// <summary>Gets what the upstream is for, for example <c>Sign-in authority</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the upstream as host or host:port. It never holds a scheme, a path, a query or credentials.</summary>
    public required string Host { get; init; }
}
