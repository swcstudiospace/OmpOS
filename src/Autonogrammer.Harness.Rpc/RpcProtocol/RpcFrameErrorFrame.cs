namespace Autonogrammer.Harness.Rpc;

/// <summary>Sent in place of a frame omp could not transmit within the transport limit.</summary>
public sealed record RpcFrameErrorFrame
{
    /// <summary>Gets the frame discriminator; always <c>rpc_frame_error</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the type of the frame that was dropped, when it could be read.</summary>
    public string? OriginalType { get; init; }

    /// <summary>Gets the reason the frame was dropped.</summary>
    public string? Error { get; init; }
}
