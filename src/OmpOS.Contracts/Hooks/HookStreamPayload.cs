using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Streaming;

namespace OmpOS.Contracts.Hooks;

/// <summary>
/// Builds and reads the <c>hook</c> stream payload. It travels as <see cref="UnknownPayload"/> with the kind preserved, the same way the
/// <c>stage</c> and <c>config_option_update</c> events do, so a client of an older contract version still deserializes the log and reads
/// the event as an unknown one.
/// </summary>
public static class HookStreamPayload
{
    private const string KindMember = "kind";

    /// <summary>Creates the log payload of one hook run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>An unknown payload with kind <see cref="HookStreamKinds.Hook"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="run"/> is null.</exception>
    public static UnknownPayload Create(HookRunEvent run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var json = JsonSerializer.Serialize(run, HooksContractsJsonContext.Default.HookRunEvent);
        var node = JsonNode.Parse(json)!.AsObject();
        node.Insert(0, KindMember, HookStreamKinds.Hook);
        using var document = JsonDocument.Parse(node.ToJsonString());
        return new UnknownPayload { OriginalKind = HookStreamKinds.Hook, Raw = document.RootElement.Clone() };
    }

    /// <summary>Reads a hook run from a log payload.</summary>
    /// <param name="payload">The payload.</param>
    /// <param name="run">The run when the payload is a well-formed <c>hook</c> event.</param>
    /// <returns>True when <paramref name="payload"/> carries a hook run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
    public static bool TryRead(StreamEventPayload payload, out HookRunEvent? run)
    {
        ArgumentNullException.ThrowIfNull(payload);
        run = null;
        if (payload is not UnknownPayload { OriginalKind: HookStreamKinds.Hook } unknown || unknown.Raw.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        try
        {
            run = unknown.Raw.Deserialize(HooksContractsJsonContext.Default.HookRunEvent);
        }
        catch (JsonException)
        {
            return false;
        }

        return run is not null;
    }
}
