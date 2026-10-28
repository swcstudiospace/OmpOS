namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The limits applied to hook configuration files. They follow the plugin manifest parser: UTF-8 JSON, a size cap and a
/// depth cap.
/// </summary>
public static class HookConfigLimits
{
    /// <summary>The largest <c>hooks.json</c> that is read, 64 KiB.</summary>
    public const int MaxHooksFileBytes = 64 * 1024;

    /// <summary>The largest Claude <c>settings.json</c> that is read, 1 MiB. Only its <c>hooks</c> key is used.</summary>
    public const int MaxClaudeSettingsBytes = 1024 * 1024;

    /// <summary>The deepest JSON nesting of a <c>hooks.json</c>.</summary>
    public const int MaxHooksJsonDepth = 16;

    /// <summary>The deepest JSON nesting of a Claude settings file.</summary>
    public const int MaxClaudeSettingsDepth = 64;

    /// <summary>The most hooks one file may declare.</summary>
    public const int MaxHooksPerFile = 256;

    /// <summary>The most elements of an <c>args</c> array.</summary>
    public const int MaxArguments = 64;

    /// <summary>The longest command, argument, prompt, matcher or environment value, in UTF-16 characters.</summary>
    public const int MaxStringLength = 16 * 1024;

    /// <summary>The largest timeout a hook may ask for, in seconds. A larger value is clamped.</summary>
    public const double MaxTimeoutSeconds = 600;

    /// <summary>The timeout of a hook that names none, in seconds. It is the existing hook runner default.</summary>
    public const double DefaultTimeoutSeconds = 30;

    /// <summary>The <c>loop_limit</c> of a Cursor-format hook that names none.</summary>
    public const int DefaultLoopLimit = 5;
}
