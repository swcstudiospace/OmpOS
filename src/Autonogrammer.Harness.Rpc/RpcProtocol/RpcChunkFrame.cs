namespace Autonogrammer.Harness.Rpc;

/// <summary>One base64 slice of a logical frame too large for a single physical line (protocol v2).</summary>
public sealed record RpcChunkFrame
{
    /// <summary>Gets the frame discriminator; always <c>rpc_chunk</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the identifier shared by every chunk of one logical frame.</summary>
    public string ChunkId { get; init; } = string.Empty;

    /// <summary>Gets this chunk's zero-based position within the logical frame.</summary>
    public int Index { get; init; }

    /// <summary>Gets the total number of chunks in the logical frame.</summary>
    public int Count { get; init; }

    /// <summary>Gets the UTF-8 byte length of the reassembled logical frame.</summary>
    public int ByteLength { get; init; }

    /// <summary>Gets the base64-encoded payload slice.</summary>
    public string Data { get; init; } = string.Empty;
}
