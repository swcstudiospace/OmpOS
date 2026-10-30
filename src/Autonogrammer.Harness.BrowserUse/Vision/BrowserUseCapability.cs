using Autonogrammer.Harness.BrowserUse.Options;

namespace Autonogrammer.Harness.BrowserUse.Vision;

/// <summary>The hub capability phase 74 reads. Offered only when both switches hold.</summary>
public static class BrowserUseCapability
{
    /// <summary>The capability name.</summary>
    public const string Name = "browser_use";

    /// <summary>Returns whether the desktop may offer the capability.</summary>
    /// <param name="options">The options.</param>
    /// <returns>True only when enabled and dispatch sessions are allowed.</returns>
    public static bool IsOffered(BrowserUseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Enabled && options.AllowDispatchSessions;
    }
}
