using System.Text.Json;
using System.Text.Json.Nodes;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Streaming;

namespace Autonogrammer.Contracts.Controls;

/// <summary>
/// Builds and reads the <c>stage</c> and <c>config_option_update</c> stream payloads. Both travel as <see cref="UnknownPayload"/> with the
/// kind preserved, so a client of an older contract version still deserializes the log and reads them as unknown events.
/// </summary>
public static class StreamEventPayloads
{
    private const string KindMember = "kind";

    /// <summary>Creates the log payload of a stage observation.</summary>
    /// <param name="stage">The stage event.</param>
    /// <returns>An unknown payload with kind <see cref="StreamEventKinds.Stage"/>.</returns>
    public static UnknownPayload CreateStage(StageEvent stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        var json = JsonSerializer.Serialize(stage, SessionControlsJsonContext.Default.StageEvent);
        return Build(StreamEventKinds.Stage, json);
    }

    /// <summary>Creates the log payload of a configuration option change.</summary>
    /// <param name="update">The update.</param>
    /// <returns>An unknown payload with kind <see cref="StreamEventKinds.ConfigOptionUpdate"/>.</returns>
    public static UnknownPayload CreateConfigOptionUpdate(ConfigOptionUpdateEvent update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var json = JsonSerializer.Serialize(update, SessionControlsJsonContext.Default.ConfigOptionUpdateEvent);
        return Build(StreamEventKinds.ConfigOptionUpdate, json);
    }

    /// <summary>Reads a stage observation from a log payload.</summary>
    /// <param name="payload">The payload.</param>
    /// <param name="stage">The stage event when the payload is a well-formed <c>stage</c> event.</param>
    /// <returns>True when <paramref name="payload"/> carries a stage event.</returns>
    public static bool TryReadStage(StreamEventPayload payload, out StageEvent? stage)
    {
        ArgumentNullException.ThrowIfNull(payload);
        stage = null;
        if (payload is not UnknownPayload { OriginalKind: StreamEventKinds.Stage } unknown)
        {
            return false;
        }

        stage = TryDeserialize(unknown.Raw, SessionControlsJsonContext.Default.StageEvent);
        return stage is not null;
    }

    /// <summary>Reads a configuration option change from a log payload.</summary>
    /// <param name="payload">The payload.</param>
    /// <param name="update">The update when the payload is a well-formed <c>config_option_update</c> event.</param>
    /// <returns>True when <paramref name="payload"/> carries a configuration option update.</returns>
    public static bool TryReadConfigOptionUpdate(StreamEventPayload payload, out ConfigOptionUpdateEvent? update)
    {
        ArgumentNullException.ThrowIfNull(payload);
        update = null;
        if (payload is not UnknownPayload { OriginalKind: StreamEventKinds.ConfigOptionUpdate } unknown)
        {
            return false;
        }

        update = TryDeserialize(unknown.Raw, SessionControlsJsonContext.Default.ConfigOptionUpdateEvent);
        return update is not null;
    }

    private static UnknownPayload Build(string kind, string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Insert(0, KindMember, kind);
        using var document = JsonDocument.Parse(node.ToJsonString());
        return new UnknownPayload { OriginalKind = kind, Raw = document.RootElement.Clone() };
    }

    private static T? TryDeserialize<T>(JsonElement raw, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            return raw.Deserialize(typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
