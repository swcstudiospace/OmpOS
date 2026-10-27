using System.Text.Json;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Builders for the hook values the tests share.
/// </summary>
internal static class HookFixtures
{
    /// <summary>Parses JSON text into a standalone element.</summary>
    /// <param name="json">The JSON text.</param>
    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Creates a session with one workspace root.</summary>
    /// <param name="root">The workspace root.</param>
    /// <param name="remote">True for a remote session.</param>
    public static HookSession Session(string root = "/work/project", bool remote = false) =>
        new("conv-1", [root])
        {
            Model = "claude-test",
            CursorVersion = "ompos-test",
            IsRemote = remote,
        };

    /// <summary>Creates a hook; every argument has a default that makes a plain consented-by-hash command hook.</summary>
    public static EffectiveHook Hook(
        HookEvent hookEvent = HookEvent.PreToolUse,
        string id = "project:hook",
        string? command = "./check.sh",
        string? matcher = null,
        bool failClosed = false,
        bool enabled = true,
        HookScope scope = HookScope.Project,
        int timeoutSeconds = 30,
        int? loopLimit = 5,
        HookLocations runsIn = HookLocations.Both,
        IReadOnlyDictionary<string, string>? env = null,
        string? hash = null,
        bool preConsented = false,
        bool? blocking = null,
        string pluginId = "cursor-project:abc",
        HookType type = HookType.Command,
        string? prompt = null,
        string workingDirectory = "/work/project",
        IReadOnlyList<string>? args = null)
    {
        _ = HookMatcher.TryCompile(matcher, out var compiled, out var problem);
        return new EffectiveHook(
            id,
            pluginId,
            scope,
            "/work/project/.cursor/hooks.json",
            HookEvents.Format(hookEvent),
            hookEvent,
            false,
            0,
            type,
            args is null ? command : null,
            args,
            args is null && type == HookType.Command ? HookShell.PosixSh : HookShell.None,
            prompt,
            null,
            TimeSpan.FromSeconds(timeoutSeconds),
            loopLimit,
            failClosed,
            matcher,
            enabled,
            null,
            env ?? new Dictionary<string, string>(StringComparer.Ordinal),
            runsIn,
            workingDirectory,
            workingDirectory,
            hash ?? id + "-hash",
            HookConsentIdentity.CurrentVersion,
            preConsented,
            blocking ?? HookEvents.IsPermission(hookEvent),
            compiled,
            problem);
    }

    /// <summary>Creates a set of hooks in the given order.</summary>
    /// <param name="hooks">The hooks, in merge order.</param>
    public static EffectiveHookSet Set(params EffectiveHook[] hooks) => new(hooks, [], []);

    /// <summary>Opens a state store that already consents to every given hash.</summary>
    /// <param name="directory">The temporary directory that holds the state file.</param>
    /// <param name="cancellationToken">The test's token.</param>
    /// <param name="consented">The hashes to consent to.</param>
    public static async Task<PluginStateStore> StateAsync(TempDirectory directory, CancellationToken cancellationToken, params string[] consented)
    {
        var store = await PluginStateStore.OpenAsync(directory.StatePath, null, cancellationToken).ConfigureAwait(true);
        foreach (var hash in consented)
        {
            await store.RecordConsentAsync(hash, cancellationToken).ConfigureAwait(true);
        }

        return store;
    }
}
