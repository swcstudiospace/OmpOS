namespace OmpOS.Presentation.Commands;

/// <summary>
/// The platforms on which a slash command is available. The members follow the UI targets in ADR-0004 (Windows, macOS,
/// Linux, iOS, Android and the web head); Linux, Windows and macOS are the three desktop heads that ADR-0006 names.
/// </summary>
[Flags]
public enum CommandPlatforms
{
    /// <summary>Available on no platform.</summary>
    None = 0,

    /// <summary>The Linux desktop head.</summary>
    Linux = 1 << 0,

    /// <summary>The Windows desktop head.</summary>
    Windows = 1 << 1,

    /// <summary>The macOS desktop head.</summary>
    MacOS = 1 << 2,

    /// <summary>The iOS head.</summary>
    Ios = 1 << 3,

    /// <summary>The Android head.</summary>
    Android = 1 << 4,

    /// <summary>The web head.</summary>
    Web = 1 << 5,
}
