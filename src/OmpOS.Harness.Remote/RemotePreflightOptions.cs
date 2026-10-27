namespace OmpOS.Harness.Remote;

/// <summary>What the remote connection test requires of the remote machine.</summary>
public sealed record RemotePreflightOptions
{
    /// <summary>
    /// Gets the oldest omp version accepted. Defaults to 18.12.0, the line the ACP behaviour OmpOS relies on (<c>session/load</c>,
    /// <c>session/new</c> with <c>mcpServers</c>) was read from (18.12.x). Older versions are untested: UNVERIFIED which release first
    /// had that behaviour.
    /// </summary>
    public Version MinimumOmpVersion { get; init; } = new(18, 12, 0);
}
