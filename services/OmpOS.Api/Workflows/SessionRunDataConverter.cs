using System.Text.Json;
using Temporalio.Converters;

namespace OmpOS.Api.Workflows;

/// <summary>
/// Temporal data converter for session-run payloads without reflection serialization. It mirrors the
/// worker's converter so the API and the worker encode workflow arguments identically.
/// </summary>
internal static class SessionRunDataConverter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = SessionRunJsonContext.Default,
    };

    /// <summary>Gets the shared session-run data converter instance.</summary>
    internal static DataConverter Instance { get; } = new DataConverter(
        new DefaultPayloadConverter(Options),
        new DefaultFailureConverter(),
        DataConverter.Default.PayloadCodec);
}
