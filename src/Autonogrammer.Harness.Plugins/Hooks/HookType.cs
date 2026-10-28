namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// How a hook decides.
/// </summary>
public enum HookType
{
    /// <summary>A command: a process that reads the event JSON on standard input and writes a decision to standard output.</summary>
    Command,

    /// <summary>A prompt: a model call that answers <c>{ ok, reason? }</c> for a natural-language policy.</summary>
    Prompt,
}
