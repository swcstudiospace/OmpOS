namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>The answer of <c>GET /settings/network</c>: the upstreams the API is configured for. The API makes no outbound call to answer, so every entry is unchecked.</summary>
public sealed record NetworkSettingsResponse
{
    /// <summary>Gets a value indicating whether any upstream was contacted. It is always false: the API sends nothing.</summary>
    public required bool Checked { get; init; }

    /// <summary>Gets one plain sentence that says why the upstreams are not checked.</summary>
    public required string Reason { get; init; }

    /// <summary>Gets the configured upstreams.</summary>
    public required IReadOnlyList<NetworkEndpointInfo> Endpoints { get; init; }
}
