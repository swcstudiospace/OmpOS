using System.Globalization;
using System.Text.Json;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Harness.AgentLoop.Tests.Stages;

namespace OmpOS.Harness.AgentLoop.Tests.Steering;

/// <summary>
/// A temporary session directory plus a fake of the omp plugin: it creates the inbox layout, refreshes <c>alive.json</c>
/// relative to the fake clock, and answers directives the way the real plugin does (read the directive file in file-name order,
/// move it to <c>done/</c>, write the acknowledgement). Nothing sleeps: the clock moves only when a test advances it.
/// </summary>
internal sealed class SteerRig : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ompos-steer-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the rig. Nothing exists on disk until <see cref="StartPlugin"/> is called.</summary>
    public SteerRig()
    {
        SessionDirectory = SessionStateLayout.SessionDirectory(_root, "s1");
        SteerDirectory = SessionStateLayout.SteerDirectory(SessionDirectory);
    }

    /// <summary>Gets the clock the channel and the fake plugin share.</summary>
    public StageFakeTimeProvider Clock { get; } = new();

    /// <summary>Gets the session directory.</summary>
    public string SessionDirectory { get; }

    /// <summary>Gets the steering directory of the session.</summary>
    public string SteerDirectory { get; }

    /// <summary>Gets the inbox directory.</summary>
    public string Inbox => Path.Combine(SteerDirectory, "inbox");

    /// <summary>Gets the acknowledgement directory.</summary>
    public string Acks => Path.Combine(SteerDirectory, "acks");

    /// <summary>Gets the directory the plugin moves handled directives to.</summary>
    public string Done => Path.Combine(SteerDirectory, "done");

    /// <summary>Gets the path of the temporary root, for tests that check no path leaks into a message.</summary>
    public string Root => _root;

    /// <summary>Creates a channel over the session.</summary>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <returns>The channel.</returns>
    public FileSteeringChannel Channel(FileSteeringChannelOptions? options = null) => new(SessionDirectory, Clock, options);

    /// <summary>Creates the steering layout and a heartbeat of the given age, as a freshly started plugin would.</summary>
    /// <param name="heartbeatAge">How long ago the plugin last refreshed <c>alive.json</c>. Zero when null.</param>
    public void StartPlugin(TimeSpan? heartbeatAge = null)
    {
        Directory.CreateDirectory(Inbox);
        Directory.CreateDirectory(Acks);
        Directory.CreateDirectory(Done);
        WriteAlive(heartbeatAge ?? TimeSpan.Zero);
    }

    /// <summary>Writes a valid heartbeat.</summary>
    /// <param name="age">How long before the clock's now the heartbeat was refreshed. Negative values lie in the future.</param>
    public void WriteAlive(TimeSpan age)
    {
        var lastSeen = (Clock.GetUtcNow() - age).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        WriteAliveRaw("{\"v\":1,\"pid\":4242,\"startedAt\":\"2026-10-26T11:00:00.000Z\",\"lastSeen\":\"" + lastSeen + "\",\"mode\":\"tui\"}");
    }

    /// <summary>Writes the content of <c>alive.json</c> as given.</summary>
    /// <param name="content">The file content.</param>
    public void WriteAliveRaw(string content)
    {
        Directory.CreateDirectory(SteerDirectory);
        File.WriteAllText(Path.Combine(SteerDirectory, "alive.json"), content);
    }

    /// <summary>Returns the names of the files in the inbox, in file-name order.</summary>
    /// <returns>The names, including any temporary file.</returns>
    public IReadOnlyList<string> InboxNames() =>
        Directory.Exists(Inbox) ? [.. Directory.EnumerateFiles(Inbox).Select(static path => Path.GetFileName(path)).Order(StringComparer.Ordinal)] : [];

    /// <summary>Reads and parses a directive file.</summary>
    /// <param name="id">The directive identifier.</param>
    /// <returns>The parsed document. The caller disposes it.</returns>
    public JsonDocument ReadDirective(string id) => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Inbox, id + ".json")));

    /// <summary>Writes an acknowledgement file with the members given.</summary>
    /// <param name="id">The directive identifier.</param>
    /// <param name="status">The status text.</param>
    /// <param name="reason">The reason code, or null.</param>
    /// <param name="mode">The mode, or null.</param>
    /// <param name="idle">The idle flag, or null.</param>
    public void WriteAck(string id, string status, string? reason = null, string? mode = "steer", bool? idle = true)
    {
        Directory.CreateDirectory(Acks);
        var text = "{\"v\":1,\"id\":\"" + id + "\",\"status\":\"" + status + "\",\"reason\":" + Quote(reason)
            + ",\"mode\":" + Quote(mode) + ",\"at\":\"2026-10-26T12:00:01.000Z\",\"idle\":" + (idle is { } flag ? (flag ? "true" : "false") : "null") + "}";
        File.WriteAllText(Path.Combine(Acks, id + ".json"), text);
    }

    /// <summary>
    /// Plays the plugin for every directive file in the inbox: reads it in file-name order, checks it against the directive schema,
    /// moves it to <c>done/</c> and writes the acknowledgement <paramref name="decide"/> asks for.
    /// </summary>
    /// <param name="decide">Maps the parsed directive text to a status and a reason code.</param>
    /// <returns>The identifiers answered, in order.</returns>
    public IReadOnlyList<string> Respond(Func<string, (string Status, string? Reason)>? decide = null)
    {
        var answered = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Inbox, "*.json").Order(StringComparer.Ordinal).ToList())
        {
            var id = Path.GetFileNameWithoutExtension(path);
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (!string.Equals(root.GetProperty("id").GetString(), id, StringComparison.Ordinal) || !string.Equals(root.GetProperty("source").GetString(), "ompos", StringComparison.Ordinal) || root.GetProperty("v").GetInt32() != 1)
            {
                throw new InvalidOperationException("The directive does not match the directive schema.");
            }

            var (status, reason) = (decide ?? Delivered)(root.GetProperty("text").GetString()!);
            File.Move(path, Path.Combine(Done, id + ".json"));
            WriteAck(id, status, reason, root.GetProperty("mode").GetString());
            answered.Add(id);
        }

        return answered;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static (string Status, string? Reason) Delivered(string text) => ("delivered", null);

    private static string Quote(string? value) => value is null ? "null" : "\"" + value + "\"";
}
