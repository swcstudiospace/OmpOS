using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Streaming;

/// <summary>
/// Base record for every stream-event payload. Deserialization never throws on
/// unknown or missing <c>kind</c> discriminators: they degrade to
/// <see cref="UnknownPayload"/> with the raw payload preserved.
/// </summary>
[JsonConverter(typeof(StreamEventPayloadConverter))]
public abstract record StreamEventPayload;
