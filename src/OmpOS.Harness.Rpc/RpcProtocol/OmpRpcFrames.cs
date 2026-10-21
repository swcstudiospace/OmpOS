using System.Text.Json;

namespace OmpOS.Harness.Rpc;

/// <summary>Wire constants and envelope reads for the omp RPC JSONL protocol (VF-2).</summary>
public static class OmpRpcFrames
{
    /// <summary>The protocol version the client negotiates after the <c>ready</c> frame.</summary>
    public const int NegotiatedProtocolVersion = 2;

    /// <summary>Maximum UTF-8 size of one physical JSONL frame, including its newline.</summary>
    public const int MaxPhysicalFrameBytes = 1024 * 1024;

    /// <summary>Maximum UTF-8 content size of one physical JSONL line, excluding its newline.</summary>
    public const int MaxPhysicalLineContentBytes = MaxPhysicalFrameBytes - 1;

    /// <summary>Maximum UTF-8 size of one logical frame reassembled from <c>rpc_chunk</c> frames.</summary>
    public const int MaxReassembledFrameBytes = 64 * 1024 * 1024;

    /// <summary>Maximum decoded payload carried by one <c>rpc_chunk</c> frame.</summary>
    public const int MaxChunkPayloadBytes = 256 * 1024;

    /// <summary>Maximum number of chunks in one reassembled frame.</summary>
    public const int MaxChunkCount = MaxReassembledFrameBytes / MaxChunkPayloadBytes;

    /// <summary>Discriminator of the handshake frame.</summary>
    public const string ReadyType = "ready";

    /// <summary>Discriminator of command responses.</summary>
    public const string ResponseType = "response";

    /// <summary>Discriminator of protocol v2 chunk frames.</summary>
    public const string ChunkType = "rpc_chunk";

    /// <summary>Discriminator of prompt completion frames.</summary>
    public const string PromptResultType = "prompt_result";

    /// <summary>Discriminator of frames omp could not send whole.</summary>
    public const string FrameErrorType = "rpc_frame_error";

    /// <summary>Reads the <c>type</c> discriminator of a frame.</summary>
    /// <param name="frame">A parsed frame.</param>
    /// <returns>The discriminator, or null when the frame is not an object or has no string <c>type</c>.</returns>
    public static string? ReadType(JsonElement frame) =>
        frame.ValueKind == JsonValueKind.Object
        && frame.TryGetProperty("type", out JsonElement type)
        && type.ValueKind == JsonValueKind.String
            ? type.GetString()
            : null;
}
