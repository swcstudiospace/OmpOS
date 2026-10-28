using System.Security.Cryptography;
using System.Text;

namespace OmpOS.Harness.AgentLoop.Launch;

/// <summary>
/// The on-disk layout of one session's private state. Every path is derived from the state root and the session
/// identifier, so a session's plugins write into one directory that no other session shares, and an observer reads only
/// the directory of the scope it was given. The methods do path math only: they never touch the file system.
/// </summary>
public static class SessionStateLayout
{
    private const int MaxSessionIdLength = 120;
    private const int MaxSlugLength = 80;
    private const int HashHexLength = 16;

    /// <summary>
    /// Returns the directory that holds one session's state: <c>&lt;stateRoot&gt;/sessions/&lt;sessionId&gt;</c>.
    /// </summary>
    /// <param name="stateRoot">The root of all session state.</param>
    /// <param name="sessionId">The session identifier: 1 to 120 letters, digits, <c>_</c>, <c>.</c> or <c>-</c>, and neither <c>.</c> nor <c>..</c>.</param>
    /// <exception cref="ArgumentException">An argument is blank, or the session identifier is not a safe single path segment.</exception>
    public static string SessionDirectory(string stateRoot, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (!IsValidSessionId(sessionId))
        {
            throw new ArgumentException(
                "A session id must be 1 to 120 letters, digits, '_', '.' or '-', and must not be '.' or '..'.",
                nameof(sessionId));
        }

        return Path.Combine(stateRoot, "sessions", sessionId);
    }

    /// <summary>
    /// Returns whether <paramref name="sessionId"/> is a safe session identifier for <see cref="SessionDirectory"/>.
    /// </summary>
    /// <param name="sessionId">The candidate identifier.</param>
    public static bool IsValidSessionId(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || sessionId.Length > MaxSessionIdLength)
        {
            return false;
        }

        if (string.Equals(sessionId, ".", StringComparison.Ordinal) || string.Equals(sessionId, "..", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in sessionId)
        {
            if (!IsAsciiLetterOrDigit(character) && character is not ('_' or '.' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the planner's state directory, the value of <c>ULTRATHINK_STATE_DIR</c>: <c>&lt;dir&gt;/ultrathink</c>.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string UltrathinkDirectory(string sessionStateDirectory) => Under(sessionStateDirectory, "ultrathink");

    /// <summary>
    /// Returns the file the planner writes for one omp session: <c>&lt;dir&gt;/ultrathink/sessions/&lt;stem&gt;.json</c>.
    /// The stem is the identifier itself when it is lowercase and plain, and otherwise a readable lowercase slug plus
    /// <c>~</c> plus the first 16 hexadecimal digits of the SHA-256 of the exact identifier, which is the planner's own rule.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    /// <param name="acpSessionId">The omp session identifier the engine reported.</param>
    /// <exception cref="ArgumentException">An argument is blank.</exception>
    public static string UltrathinkSessionFile(string sessionStateDirectory, string acpSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionStateDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(acpSessionId);
        return Path.Combine(UltrathinkDirectory(sessionStateDirectory), "sessions", SessionFileStem(acpSessionId) + ".json");
    }

    /// <summary>
    /// Returns the orchestrator's state directory, the value of <c>SWARM_DIR</c>: <c>&lt;dir&gt;/swarm</c>.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string SwarmDirectory(string sessionStateDirectory) => Under(sessionStateDirectory, "swarm");

    /// <summary>
    /// Returns the orchestrator's live snapshot file: <c>&lt;dir&gt;/swarm/.swarm/live.json</c>.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string SwarmLiveFile(string sessionStateDirectory) =>
        Path.Combine(SwarmDirectory(sessionStateDirectory), ".swarm", "live.json");

    /// <summary>
    /// Returns the orchestrator's event log: <c>&lt;dir&gt;/swarm/.swarm/events.jsonl</c>.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string SwarmEventsFile(string sessionStateDirectory) =>
        Path.Combine(SwarmDirectory(sessionStateDirectory), ".swarm", "events.jsonl");

    /// <summary>
    /// Returns the file the orchestrator mirrors its GSD state lines to, the value of <c>SWARM_GSD_STATE_FILE</c>:
    /// <c>&lt;dir&gt;/swarm/gsd-state.md</c>. It is always a path under the session directory, never an empty value, because the
    /// pseudo-terminal backend removes a variable whose value is empty and the orchestrator would then write into the workspace's
    /// <c>.planning/STATE.md</c>.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string SwarmGsdStateFile(string sessionStateDirectory) =>
        Path.Combine(SwarmDirectory(sessionStateDirectory), "gsd-state.md");

    /// <summary>
    /// Returns the directory of the planner's host state, the value of <c>SWARM_CLAUDE_STATE_DIR</c>: <c>&lt;dir&gt;/claude-state</c>.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string ClaudeStateDirectory(string sessionStateDirectory) => Under(sessionStateDirectory, "claude-state");

    /// <summary>
    /// Returns the directory the substrate skills extension writes pending skills to, the value of
    /// <c>SUBSTRATE_OMP_PENDING_DIR</c>: <c>&lt;dir&gt;/substrate-pending</c>.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string SubstratePendingDirectory(string sessionStateDirectory) => Under(sessionStateDirectory, "substrate-pending");

    /// <summary>
    /// Returns the steering directory of the session, the value of <c>OMPOS_STEER_DIR</c>: <c>&lt;dir&gt;/steer</c>. The omp
    /// plugin serves <c>inbox/</c>, <c>acks/</c>, <c>done/</c> and <c>alive.json</c> inside it.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string SteerDirectory(string sessionStateDirectory) => Under(sessionStateDirectory, "steer");

    /// <summary>
    /// Returns the file omp's plugin writes its connection report to, the value of <c>OMPOS_CONNECTION_FILE</c>:
    /// <c>&lt;dir&gt;/connection/report.json</c>.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string ConnectionFile(string sessionStateDirectory) =>
        Path.Combine(Under(sessionStateDirectory, "connection"), "report.json");

    /// <summary>
    /// Returns the directory of the session's hooks state: <c>&lt;dir&gt;/hooks</c>. It holds the bridge startup file and the run log, and is
    /// created private to the owner (mode 0700) by the bridge server and the run log.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string HooksDirectory(string sessionStateDirectory) => Under(sessionStateDirectory, "hooks");

    /// <summary>
    /// Returns the startup file the hook bridge writes for the omp extension, the value of <c>OMPOS_HOOKS_STARTUP_FILE</c>:
    /// <c>&lt;dir&gt;/hooks/startup.json</c>. It holds event names only, never the bearer token.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string HooksStartupFile(string sessionStateDirectory) => Path.Combine(HooksDirectory(sessionStateDirectory), "startup.json");

    /// <summary>
    /// Returns the append-only run log of the session's hooks: <c>&lt;dir&gt;/hooks/runs.jsonl</c> (the previous generation is <c>runs.jsonl.1</c>).
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string HooksRunLogFile(string sessionStateDirectory) => Path.Combine(HooksDirectory(sessionStateDirectory), "runs.jsonl");

    /// <summary>
    /// Returns the path of the omp settings overlay of the session: <c>&lt;dir&gt;/omp-overlay.yml</c>.
    /// </summary>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionDirectory"/>.</param>
    public static string ConfigOverlayFile(string sessionStateDirectory) => Under(sessionStateDirectory, "omp-overlay.yml");

    private static string Under(string sessionStateDirectory, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionStateDirectory);
        return Path.Combine(sessionStateDirectory, name);
    }

    private static string SessionFileStem(string id)
    {
        if (IsPlainStem(id))
        {
            return id;
        }

        var slug = new StringBuilder(MaxSlugLength);
        foreach (var rune in id.EnumerateRunes())
        {
            if (slug.Length >= MaxSlugLength)
            {
                break;
            }

            if (rune.IsAscii && (IsAsciiLetterOrDigit((char)rune.Value) || rune.Value is '_' or '.' or '-'))
            {
                slug.Append(char.ToLowerInvariant((char)rune.Value));
            }
            else
            {
                slug.Append('_');
            }
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
        return slug.Append('~').Append(hash, 0, HashHexLength).ToString();
    }

    private static bool IsPlainStem(string id)
    {
        if (id.Length > MaxSessionIdLength)
        {
            return false;
        }

        foreach (var character in id)
        {
            var plain = character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.' or '-';
            if (!plain)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetterOrDigit(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
}
