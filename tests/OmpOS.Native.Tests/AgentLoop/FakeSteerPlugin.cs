using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OmpOS.Harness.AgentLoop.Launch;
using static OmpOS.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace OmpOS.Native.Tests.AgentLoop;

/// <summary>
/// The omp side of the steering channel, played by a test: it writes the heartbeat the channel needs to count the plugin as serving,
/// reads the directives the head queued, and writes the acknowledgement files the real plugin would. No omp process is involved.
/// </summary>
internal static class FakeSteerPlugin
{
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Writes a heartbeat stamped <paramref name="now"/>, as the plugin does while omp runs.</summary>
    /// <param name="sessionDirectory">The state directory of the session.</param>
    /// <param name="now">The time of the heartbeat.</param>
    public static void WriteAlive(string sessionDirectory, DateTimeOffset now)
    {
        var stamp = Format(now);
        var alive = new JsonObject
        {
            ["v"] = 1,
            ["pid"] = 1,
            ["startedAt"] = stamp,
            ["lastSeen"] = stamp,
            ["mode"] = "acp",
        };
        File.WriteAllText(Path.Combine(SessionStateLayout.SteerDirectory(sessionDirectory), "alive.json"), alive.ToJsonString(), Utf8);
    }

    /// <summary>Returns the directives queued in the inbox, as parsed JSON, in file-name order.</summary>
    /// <param name="sessionDirectory">The state directory of the session.</param>
    /// <returns>The directives.</returns>
    public static IReadOnlyList<JsonObject> Directives(string sessionDirectory)
    {
        var inbox = Path.Combine(SessionStateLayout.SteerDirectory(sessionDirectory), "inbox");
        if (!Directory.Exists(inbox))
        {
            return [];
        }

        return
        [
            .. Directory.EnumerateFiles(inbox, "*.json")
                .Order(StringComparer.Ordinal)
                .Select(static file => JsonNode.Parse(File.ReadAllText(file, Utf8))!.AsObject()),
        ];
    }

    /// <summary>Waits until a directive is queued, then writes the acknowledgement for it with the given status and reason.</summary>
    /// <param name="sessionDirectory">The state directory of the session.</param>
    /// <param name="status">The status code: <c>delivered</c>, <c>rejected</c> or <c>failed</c>.</param>
    /// <param name="reason">The reason code, or null for none.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <param name="idle">What the acknowledgement says about the session at delivery: true when it had no running turn, false when it had one, null when the plugin cannot tell.</param>
    /// <returns>The directive that was acknowledged.</returns>
    public static async Task<JsonObject> AckNextAsync(string sessionDirectory, string status, string? reason, CancellationToken cancellationToken, bool? idle = false)
    {
        await EventuallyAsync(() => Directives(sessionDirectory).Count > 0, "a queued steering directive", cancellationToken).ConfigureAwait(true);
        var directive = Directives(sessionDirectory)[0];
        var id = directive["id"]!.GetValue<string>();
        var acks = Path.Combine(SessionStateLayout.SteerDirectory(sessionDirectory), "acks");
        var ack = new JsonObject
        {
            ["v"] = 1,
            ["id"] = id,
            ["status"] = status,
            ["reason"] = reason,
            ["mode"] = directive["mode"]!.GetValue<string>(),
            ["at"] = Format(DateTimeOffset.UtcNow),
            ["idle"] = idle,
        };
        var temporary = Path.Combine(acks, id + ".tmp");
        await File.WriteAllTextAsync(temporary, ack.ToJsonString(), Utf8, cancellationToken).ConfigureAwait(true);
        File.Move(temporary, Path.Combine(acks, id + ".json"));
        return directive;
    }

    private static string Format(DateTimeOffset time) => time.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture);
}
