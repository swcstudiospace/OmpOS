namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Error frame in either direction.</summary>
public sealed record DispatchErrFrame
{
    /// <summary>Protocol version. Always 1.</summary>
    public required int V { get; init; }

    /// <summary>Frame type <c>err</c>.</summary>
    public required string T { get; init; }

    /// <summary>Error code. Unknown frame type uses <c>unsupported_frame</c>.</summary>
    public required string Code { get; init; }

    /// <summary>Optional detail. Never a payload.</summary>
    public string? Detail { get; init; }
}
