namespace Autonogrammer.Harness.BrowserUse;

/// <summary>Which lifetime bound a session host refused.</summary>
public enum BrowserSessionLimit
{
    /// <summary>More than <c>MaxConcurrentBrowsers</c> were requested.</summary>
    ConcurrentBrowsers = 0,

    /// <summary>More than <c>MaxTabs</c> were requested.</summary>
    Tabs = 1,

    /// <summary>The working set exceeded <c>MaxBrowserMemoryMiB</c>.</summary>
    Memory = 2,

    /// <summary>The session was idle past <c>IdleTimeout</c>.</summary>
    Idle = 3,

    /// <summary>The session exceeded <c>MaxSessionLifetime</c>.</summary>
    WallTime = 4,

    /// <summary>The profile path used a character outside the allowed set.</summary>
    ProfilePathUnsafe = 5,
}
