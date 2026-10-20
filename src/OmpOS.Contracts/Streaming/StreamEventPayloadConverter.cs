using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Streaming;

/// <summary>
/// Source-generation-safe JSON converter for <see cref="StreamEventPayload"/>.
/// Same shape as <c>UiBlockConverter</c>: known kinds resolve through the
/// source-generated derived <c>JsonTypeInfo</c> lookups; unknown or missing
/// kinds degrade to <see cref="UnknownPayload"/> and never throw.
/// </summary>
public sealed class StreamEventPayloadConverter : JsonConverter<StreamEventPayload>
{
    private const string Discriminator = "kind";

    private static readonly Dictionary<string, Type> KindToType = new(StringComparer.Ordinal)
    {
        ["text_delta"] = typeof(TextDeltaPayload),
        ["ui_block"] = typeof(UiBlockPayload),
        ["error"] = typeof(ErrorPayload),
        ["completed"] = typeof(CompletedPayload),
        ["unknown"] = typeof(UnknownPayload),
    };

    private static readonly Dictionary<Type, string> TypeToKind = KindToType.ToDictionary(static pair => pair.Value, static pair => pair.Key);

    /// <inheritdoc />
    public override StreamEventPayload? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        string? kind = root.TryGetProperty(Discriminator, out var kindElement) && kindElement.ValueKind == JsonValueKind.String
            ? kindElement.GetString()
            : null;
        if (kind is not null && KindToType.TryGetValue(kind, out var type) && type != typeof(UnknownPayload))
        {
            return (StreamEventPayload?)root.Deserialize(options.GetTypeInfo(type));
        }

        return new UnknownPayload { OriginalKind = kind, Raw = root.Clone() };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, StreamEventPayload value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        if (value is UnknownPayload { Raw.ValueKind: JsonValueKind.Object } unknown)
        {
            unknown.Raw.WriteTo(writer);
            return;
        }

        var type = value.GetType();
        var node = JsonSerializer.SerializeToNode(value, options.GetTypeInfo(type)) as JsonObject
            ?? throw new JsonException($"Cannot serialize {type}.");
        node.Insert(0, Discriminator, TypeToKind[type]);
        node.WriteTo(writer, options);
    }
}
