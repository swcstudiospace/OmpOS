namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The file format of a hook configuration.
/// </summary>
public enum HookConfigFormat
{
    /// <summary>Cursor's <c>hooks.json</c>: <c>{ "version": 1, "hooks": { "event": [ { "command": "..." } ] } }</c>.</summary>
    HooksJson,

    /// <summary>
    /// A Claude Code <c>settings.json</c>: only its <c>hooks</c> key is read, in the nested layout
    /// <c>{ "hooks": { "PreToolUse": [ { "matcher": "", "hooks": [ { "type": "command", "command": "..." } ] } ] } }</c>.
    /// </summary>
    ClaudeSettings,
}
