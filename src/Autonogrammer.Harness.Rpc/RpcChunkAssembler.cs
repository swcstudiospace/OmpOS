using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Rpc;

/// <summary>
/// Reassembles protocol v2 <c>rpc_chunk</c> frames into logical frames. Chunks may arrive in any index order; a
/// logical frame is released once every index is present. Frames that are not chunks pass through unchanged, so an
/// unrelated frame interleaved with a chunk stream is delivered rather than lost.
/// </summary>
/// <remarks>Not thread-safe: the single reader of the transport calls it.</remarks>
public sealed class RpcChunkAssembler
{
    private const int CompletedIdMemory = 64;
    private const int MaxIncompleteGroups = 64;
    private const int MaxChunkIdLength = 128;

    private readonly Dictionary<string, PendingFrame> _pending = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private readonly Queue<string> _completedOrder = new();
    private long _bufferedBytes;

    /// <summary>Accepts one physical JSONL line.</summary>
    /// <param name="line">The line without its trailing newline.</param>
    /// <returns>
    /// The logical frame when the line is a complete frame or is not a chunk; null while chunks of a frame are still
    /// buffered. The caller owns and disposes the returned document.
    /// </returns>
    /// <exception cref="MalformedFrameHarnessException">The line breaks a size cap, the JSON envelope, or the chunk sequence rules.</exception>
    public JsonDocument? Accept(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Length > OmpRpcFrames.MaxPhysicalLineContentBytes
            || Encoding.UTF8.GetByteCount(line) > OmpRpcFrames.MaxPhysicalLineContentBytes)
        {
            throw new MalformedFrameHarnessException("A physical frame exceeds the 1 MiB transport limit.");
        }

        JsonDocument document = ParseObject(line);
        if (!string.Equals(OmpRpcFrames.ReadType(document.RootElement), OmpRpcFrames.ChunkType, StringComparison.Ordinal))
        {
            return document;
        }

        try
        {
            return AcceptChunk(document.RootElement);
        }
        finally
        {
            document.Dispose();
        }
    }

    private static JsonDocument ParseObject(string line)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            throw new MalformedFrameHarnessException("A physical frame is not valid JSON.", null, ex);
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            return document;
        }

        document.Dispose();
        throw new MalformedFrameHarnessException("An rpc frame must be a JSON object.");
    }

    private JsonDocument? AcceptChunk(JsonElement chunk)
    {
        string chunkId = ReadChunkId(chunk);
        int index = ReadInt(chunk, "index");
        int count = ReadInt(chunk, "count");
        int byteLength = ReadInt(chunk, "byteLength");
        ValidateMetadata(index, count, byteLength);
        byte[] payload = DecodePayload(chunk);

        if (!_pending.TryGetValue(chunkId, out PendingFrame? pending))
        {
            if (_completed.Contains(chunkId))
            {
                throw new MalformedFrameHarnessException($"rpc_chunk id '{chunkId}' was reused after its frame completed.");
            }

            if (_pending.Count >= MaxIncompleteGroups)
            {
                throw new MalformedFrameHarnessException(
                    $"Too many incomplete rpc_chunk groups; at most {MaxIncompleteGroups} may be in flight.");
            }

            pending = new PendingFrame(count, byteLength);
            _pending.Add(chunkId, pending);
        }
        else if (pending.Count != count || pending.ByteLength != byteLength)
        {
            throw new MalformedFrameHarnessException($"rpc_chunk '{chunkId}' changed its count or byteLength mid-frame.");
        }

        if (pending.Parts[index] is not null)
        {
            throw new MalformedFrameHarnessException($"rpc_chunk '{chunkId}' repeated index {index}.");
        }

        if (_bufferedBytes + payload.Length > OmpRpcFrames.MaxReassembledFrameBytes)
        {
            throw new MalformedFrameHarnessException("Buffered rpc_chunk payloads exceed the 64 MiB reassembly limit.");
        }

        pending.Parts[index] = payload;
        pending.Received++;
        pending.ReceivedBytes += payload.Length;
        _bufferedBytes += payload.Length;
        if (pending.ReceivedBytes > pending.ByteLength)
        {
            throw new MalformedFrameHarnessException($"rpc_chunk '{chunkId}' carries more bytes than its declared byteLength.");
        }

        return pending.Received < pending.Count ? null : Complete(chunkId, pending);
    }

    private JsonDocument Complete(string chunkId, PendingFrame pending)
    {
        _pending.Remove(chunkId);
        _bufferedBytes -= pending.ReceivedBytes;
        RememberCompleted(chunkId);

        if (pending.ReceivedBytes != pending.ByteLength)
        {
            throw new MalformedFrameHarnessException($"rpc_chunk '{chunkId}' reassembled to the wrong length.");
        }

        byte[] logical = new byte[pending.ByteLength];
        int offset = 0;
        foreach (byte[]? part in pending.Parts)
        {
            part!.CopyTo(logical, offset);
            offset += part.Length;
        }

        JsonDocument document = ParseLogical(logical);
        if (string.Equals(OmpRpcFrames.ReadType(document.RootElement), OmpRpcFrames.ChunkType, StringComparison.Ordinal))
        {
            document.Dispose();
            throw new MalformedFrameHarnessException("A reassembled rpc frame may not itself be an rpc_chunk.");
        }

        return document;
    }

    private static JsonDocument ParseLogical(byte[] logical)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(logical);
        }
        catch (JsonException ex)
        {
            throw new MalformedFrameHarnessException("A reassembled rpc frame is not valid JSON.", null, ex);
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            return document;
        }

        document.Dispose();
        throw new MalformedFrameHarnessException("A reassembled rpc frame must be a JSON object.");
    }

    private void RememberCompleted(string chunkId)
    {
        _completed.Add(chunkId);
        _completedOrder.Enqueue(chunkId);
        while (_completedOrder.Count > CompletedIdMemory)
        {
            _completed.Remove(_completedOrder.Dequeue());
        }
    }

    private static string ReadChunkId(JsonElement chunk)
    {
        if (chunk.TryGetProperty("chunkId", out JsonElement id)
            && id.ValueKind == JsonValueKind.String
            && id.GetString() is { Length: > 0 and <= MaxChunkIdLength } text)
        {
            return text;
        }

        throw new MalformedFrameHarnessException("rpc_chunk chunkId must be a non-empty string of at most 128 characters.");
    }

    private static int ReadInt(JsonElement chunk, string property)
    {
        if (chunk.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int number))
        {
            return number;
        }

        throw new MalformedFrameHarnessException($"rpc_chunk {property} is missing or not a 32-bit integer.");
    }

    private static void ValidateMetadata(int index, int count, int byteLength)
    {
        if (count < 2 || count > OmpRpcFrames.MaxChunkCount)
        {
            throw new MalformedFrameHarnessException($"rpc_chunk count {count} is outside 2..{OmpRpcFrames.MaxChunkCount}.");
        }

        if (index < 0 || index >= count)
        {
            throw new MalformedFrameHarnessException($"rpc_chunk index {index} is outside 0..{count - 1}.");
        }

        if (byteLength <= OmpRpcFrames.MaxPhysicalFrameBytes || byteLength > OmpRpcFrames.MaxReassembledFrameBytes)
        {
            throw new MalformedFrameHarnessException("rpc_chunk byteLength must exceed 1 MiB and fit the 64 MiB limit.");
        }
    }

    private static byte[] DecodePayload(JsonElement chunk)
    {
        if (!chunk.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.String)
        {
            throw new MalformedFrameHarnessException("rpc_chunk data must be a base64 string.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(data.GetString()!);
        }
        catch (FormatException ex)
        {
            throw new MalformedFrameHarnessException("rpc_chunk data is not valid base64.", null, ex);
        }

        if (bytes.Length == 0 || bytes.Length > OmpRpcFrames.MaxChunkPayloadBytes)
        {
            throw new MalformedFrameHarnessException("rpc_chunk payload is empty or exceeds 256 KiB.");
        }

        return bytes;
    }

    /// <summary>Chunks received so far for one logical frame, held until every index arrives.</summary>
    private sealed class PendingFrame(int count, int byteLength)
    {
        public int Count { get; } = count;

        public int ByteLength { get; } = byteLength;

        public byte[]?[] Parts { get; } = new byte[count][];

        public int Received { get; set; }

        public int ReceivedBytes { get; set; }
    }
}
