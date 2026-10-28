namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The operating system family that decides the managed hook path and the shell.
/// </summary>
public enum HookPlatform
{
    /// <summary>Linux, including WSL.</summary>
    Linux,

    /// <summary>macOS.</summary>
    MacOs,

    /// <summary>Windows.</summary>
    Windows,
}
