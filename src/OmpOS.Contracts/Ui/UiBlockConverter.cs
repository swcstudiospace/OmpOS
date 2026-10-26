using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Ui;

/// <summary>
/// Source-generation-safe JSON converter for <see cref="UiBlock"/>.
/// Known kinds resolve through the source-generated derived <c>JsonTypeInfo</c>
/// lookups (AOT-safe, no reflection fallback); unknown or missing kinds degrade
/// to <see cref="UnknownBlock"/> and never throw.
/// </summary>
public sealed class UiBlockConverter : JsonConverter<UiBlock>
{
    private const string Discriminator = "kind";

    private static readonly Dictionary<string, Type> KindToType = new(StringComparer.Ordinal)
    {
        ["markdown"] = typeof(MarkdownBlock),
        ["code"] = typeof(CodeBlock),
        ["tool_call"] = typeof(ToolCallBlock),
        ["permission"] = typeof(PermissionBlock),
        ["plan"] = typeof(PlanBlock),
        ["diff"] = typeof(DiffPlaceholderBlock),
        ["error"] = typeof(ErrorBlock),
        ["a2ui-surface"] = typeof(A2uiSurfaceBlock),
        ["unknown"] = typeof(UnknownBlock),
    };

    // Folded-only blocks serialize under their kind so a folded tree can be written. Read never maps the kind back:
    // a wire "user" degrades to UnknownBlock like every kind that deserialization does not produce.
    private static readonly Dictionary<Type, string> TypeToKind = new(KindToType.ToDictionary(static pair => pair.Value, static pair => pair.Key))
    {
        [typeof(UserMessageBlock)] = "user",
    };

    /// <inheritdoc />
    public override UiBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        // A non-object root (array, string, number, true/false, null) has no
        // discriminator and must degrade to UnknownBlock, never throw.
        string? kind = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(Discriminator, out var kindElement)
            && kindElement.ValueKind == JsonValueKind.String
                ? kindElement.GetString()
                : null;
        if (kind is not null && KindToType.TryGetValue(kind, out var type) && type != typeof(UnknownBlock))
        {
            return (UiBlock?)root.Deserialize(options.GetTypeInfo(type));
        }

        var id = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("id", out var idElement)
            && idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString() ?? string.Empty
                : string.Empty;
        return new UnknownBlock { Id = id, OriginalKind = kind, Raw = root.Clone() };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, UiBlock value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        if (value is UnknownBlock { Raw.ValueKind: not JsonValueKind.Undefined } unknown)
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
