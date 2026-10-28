namespace OmpOS.Contracts.Cloud;

/// <summary>The body of <c>GET /cloud/secrets</c>: the caller's secrets by name.</summary>
public sealed record CloudSecretListResponse
{
    /// <summary>Gets the secrets, ordered by name.</summary>
    public required IReadOnlyList<CloudSecretInfo> Secrets { get; init; }
}
