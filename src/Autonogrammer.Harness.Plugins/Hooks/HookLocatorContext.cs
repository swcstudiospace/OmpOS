namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The inputs of <see cref="HookSourceLocator"/>: everything environment-specific, so the path table can be tested for
/// every operating system without touching the real machine.
/// </summary>
/// <param name="WorkspaceRoots">The absolute workspace roots; a multi-root workspace lists several.</param>
/// <param name="HomeDirectory">The user's home directory, or null when unknown (user scopes are then skipped).</param>
/// <param name="Platform">The operating system family.</param>
/// <param name="IsRemote">True for remote and hosted sessions, where user scopes do not apply (Cursor parity).</param>
/// <param name="IncludeThirdPartyConfigs">True to also read Claude settings files. Discovery is not trust: consent still applies.</param>
/// <param name="ProgramDataDirectory">The Windows ProgramData directory; used only on <see cref="HookPlatform.Windows"/>.</param>
public sealed record HookLocatorContext(
    IReadOnlyList<string> WorkspaceRoots,
    string? HomeDirectory,
    HookPlatform Platform,
    bool IsRemote = false,
    bool IncludeThirdPartyConfigs = true,
    string ProgramDataDirectory = @"C:\ProgramData");
