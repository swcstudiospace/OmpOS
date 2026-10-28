namespace Autonogrammer.Harness.Rpc;

/// <summary>The first frame omp writes: its protocol versions and size limits.</summary>
public sealed record ReadyFrame
{
    /// <summary>Gets the frame discriminator; always <c>ready</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the protocol version the engine starts in.</summary>
    public int ProtocolVersion { get; init; }

    /// <summary>Gets the protocol versions the engine accepts in <c>negotiate_protocol</c>.</summary>
    public IReadOnlyList<int> SupportedProtocolVersions { get; init; } = [];

    /// <summary>Gets the maximum UTF-8 size of one physical frame, including its newline.</summary>
    public int MaxFrameBytes { get; init; }

    /// <summary>Gets the maximum UTF-8 size of one reassembled logical frame.</summary>
    public int MaxReassembledFrameBytes { get; init; }
}
