using System.Collections;
using System.Collections.Frozen;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Builds the environment a hook process receives. The base is an allow-list of the parent's variables, not the whole
/// parent environment, so an API key or token in the host's environment never reaches a repository's hook script (ADR-0013
/// risk 1). On top of it come the variables Cursor documents (<c>CURSOR_PROJECT_DIR</c>, <c>CURSOR_VERSION</c>,
/// <c>CURSOR_USER_EMAIL</c>, <c>CURSOR_TRANSCRIPT_PATH</c>, <c>CURSOR_CODE_REMOTE</c>, <c>CURSOR_PLUGIN_ROOT</c>,
/// <c>CLAUDE_PROJECT_DIR</c>), the Autonogrammer aliases, the environment <c>sessionStart</c> hooks returned and finally the hook's own
/// <c>env</c>. Variables named <c>AUTONOGRAMMER_*</c>, <c>CURSOR_*</c> and <c>CLAUDE_PROJECT_DIR</c> cannot be set by hook or session
/// environment, so a hook cannot forge the identity variables.
/// </summary>
public static class HookEnvironment
{
    private static readonly FrozenSet<string> Allowed = new[]
    {
        "PATH", "HOME", "USER", "LOGNAME", "SHELL", "LANG", "LANGUAGE", "TERM", "TMPDIR", "TEMP", "TMP", "TZ",
        "USERPROFILE", "SYSTEMROOT", "WINDIR", "COMSPEC", "PATHEXT", "APPDATA", "LOCALAPPDATA", "PROGRAMDATA",
        "PROGRAMFILES", "PROGRAMFILES(X86)", "XDG_RUNTIME_DIR",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the current process environment as a dictionary.
    /// </summary>
    /// <returns>A snapshot of the environment variables.</returns>
    public static IReadOnlyDictionary<string, string> CaptureParent()
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name && entry.Value is string value)
            {
                snapshot[name] = value;
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Builds the child environment for one hook run.
    /// </summary>
    /// <param name="parent">The parent environment to take the allow-list from.</param>
    /// <param name="inheritEnvironment">True to pass the whole parent environment (Cursor-like) except the bridge token; false for the allow-list.</param>
    /// <param name="hook">The hook that runs.</param>
    /// <param name="session">The session the event belongs to.</param>
    /// <param name="eventName">The event name for <c>AUTONOGRAMMER_HOOK_EVENT</c>.</param>
    /// <returns>The complete environment of the child.</returns>
    public static IReadOnlyDictionary<string, string> Build(
        IReadOnlyDictionary<string, string> parent,
        bool inheritEnvironment,
        EffectiveHook hook,
        HookSession session,
        string eventName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(eventName);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in parent)
        {
            if (IsReserved(pair.Key))
            {
                continue;
            }

            if (inheritEnvironment || Allowed.Contains(pair.Key) || pair.Key.StartsWith("LC_", StringComparison.Ordinal))
            {
                environment[pair.Key] = pair.Value;
            }
        }

        var project = hook.ScopeRoot.Length > 0 && string.IsNullOrEmpty(session.ProjectDirectory) ? hook.ScopeRoot : session.ProjectDirectory;
        environment["CURSOR_PROJECT_DIR"] = project;
        environment["CLAUDE_PROJECT_DIR"] = project;
        environment["AUTONOGRAMMER_PROJECT_DIR"] = project;
        environment["CURSOR_VERSION"] = session.CursorVersion;
        environment["AUTONOGRAMMER_VERSION"] = session.CursorVersion;
        environment["AUTONOGRAMMER_HOOK_EVENT"] = eventName;
        environment["AUTONOGRAMMER_SESSION_ID"] = session.ConversationId;
        if (session.UserEmail is not null)
        {
            environment["CURSOR_USER_EMAIL"] = session.UserEmail;
        }

        if (session.TranscriptPath is not null)
        {
            environment["CURSOR_TRANSCRIPT_PATH"] = session.TranscriptPath;
        }

        if (session.IsRemote)
        {
            environment["CURSOR_CODE_REMOTE"] = "true";
        }

        if (hook.Scope == HookScope.Plugin)
        {
            environment["CURSOR_PLUGIN_ROOT"] = hook.ScopeRoot;
            environment["AUTONOGRAMMER_PLUGIN_ROOT"] = hook.ScopeRoot;
        }

        Overlay(environment, session.SessionEnvironment);
        Overlay(environment, hook.Env);
        return environment;
    }

    /// <summary>
    /// True for a variable name that hook and session environment may not set.
    /// </summary>
    /// <param name="name">The variable name.</param>
    public static bool IsReserved(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.StartsWith("AUTONOGRAMMER_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("CURSOR_", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "CLAUDE_PROJECT_DIR", StringComparison.OrdinalIgnoreCase);
    }

    private static void Overlay(Dictionary<string, string> environment, IReadOnlyDictionary<string, string> values)
    {
        foreach (var pair in values)
        {
            if (!IsReserved(pair.Key))
            {
                environment[pair.Key] = pair.Value;
            }
        }
    }
}
