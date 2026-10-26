using System.Globalization;
using System.Text.Json;

namespace OmpOS.Harness.AgentLoop.Tests.Live;

/// <summary>
/// The steering heartbeat the omp plugin writes to <c>alive.json</c> in <c>OMPOS_STEER_DIR</c>:
/// <c>{"v":1,"pid","startedAt","lastSeen","mode"}</c>, refreshed every 5 s. OmpOS treats a <c>lastSeen</c> older than 15 s as
/// "not serving".
/// </summary>
/// <param name="Pid">The process identifier of the omp process that serves the inbox, or null.</param>
/// <param name="StartedAt">The time the plugin started serving, or <see cref="DateTimeOffset.MinValue"/> when absent.</param>
/// <param name="LastSeen">The time of the latest refresh.</param>
/// <param name="Mode">The run mode omp reports (<c>tui</c>, <c>rpc</c>, <c>json</c> or <c>print</c>), or null.</param>
internal sealed record LiveHeartbeat(long? Pid, DateTimeOffset StartedAt, DateTimeOffset LastSeen, string? Mode)
{
    /// <summary>The name of the heartbeat file inside the steering directory.</summary>
    public const string FileName = "alive.json";

    /// <summary>Reads a heartbeat.</summary>
    /// <param name="utf8">The file content.</param>
    /// <returns>The heartbeat, or null when the content is not a readable heartbeat with a <c>lastSeen</c> time.</returns>
    public static LiveHeartbeat? TryParse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty)
        {
            return null;
        }

        try
        {
            var reader = new Utf8JsonReader(utf8);
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryReadTime(root, "lastSeen", out var lastSeen))
            {
                return null;
            }

            if (!TryReadTime(root, "startedAt", out var startedAt))
            {
                startedAt = DateTimeOffset.MinValue;
            }

            long? pid = root.TryGetProperty("pid", out var pidElement)
                && pidElement.ValueKind == JsonValueKind.Number
                && pidElement.TryGetInt64(out var pidValue)
                    ? pidValue
                    : null;
            var mode = root.TryGetProperty("mode", out var modeElement) && modeElement.ValueKind == JsonValueKind.String
                ? modeElement.GetString()
                : null;
            return new LiveHeartbeat(pid, startedAt, lastSeen, mode);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadTime(JsonElement root, string name, out DateTimeOffset value)
    {
        value = default;
        return root.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                element.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out value);
    }
}
