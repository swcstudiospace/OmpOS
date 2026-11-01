namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Body of <c>GET /dispatch/commands</c>.</summary>
public sealed record DispatchCommandListResponse
{
    /// <summary>Commands owned by the caller.</summary>
    public required IReadOnlyList<DispatchCommandDto> Commands { get; init; }
}
