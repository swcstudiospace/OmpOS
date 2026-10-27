namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Maps the running operating system to the values the hook code depends on.
/// </summary>
public static class HookPlatforms
{
    /// <summary>
    /// The family of the operating system this process runs on.
    /// </summary>
    public static HookPlatform Current =>
        OperatingSystem.IsWindows() ? HookPlatform.Windows
        : OperatingSystem.IsMacOS() ? HookPlatform.MacOs
        : HookPlatform.Linux;

    /// <summary>
    /// Returns the shell a hook <c>command</c> string runs in on a platform.
    /// </summary>
    /// <param name="platform">The operating system family.</param>
    /// <returns><see cref="HookShell.PowerShell"/> on Windows, otherwise <see cref="HookShell.PosixSh"/>.</returns>
    public static HookShell ShellFor(HookPlatform platform) =>
        platform == HookPlatform.Windows ? HookShell.PowerShell : HookShell.PosixSh;
}
