using System.Text.Json;
using Temporalio.Converters;

namespace OmpOS.Workflows.Worker;

/// <summary>
/// Temporal data converter for session-run payloads without reflection serialization.
/// </summary>
/// <remarks>
/// Wraps the standard <see cref="DefaultPayloadConverter"/> (binary, null, protobuf, JSON
/// encodings) with <see cref="JsonSerializerOptions"/> bound to
/// <see cref="SessionRunPayloadJsonContext"/>. Used by the worker client
/// (<c>TemporalClientConnectOptions.DataConverter</c>) and the replay tests
/// (<c>WorkflowReplayerOptions.DataConverter</c>).
/// </remarks>
internal static class SessionRunDataConverter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = SessionRunPayloadJsonContext.Default,
    };

    /// <summary>Gets the shared session-run data converter instance.</summary>
    internal static DataConverter Instance { get; } = new DataConverter(
        new DefaultPayloadConverter(Options),
        new DefaultFailureConverter(),
        DataConverter.Default.PayloadCodec);
}
