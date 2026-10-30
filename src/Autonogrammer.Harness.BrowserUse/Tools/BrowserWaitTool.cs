namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Waits at most 10 seconds.</summary>
public static class BrowserWaitTool
{
    /// <summary>The tool name.</summary>
    public const string Name = "browser_wait";

    /// <summary>The cap.</summary>
    public static readonly TimeSpan Maximum = TimeSpan.FromSeconds(10);

    /// <summary>Caps the requested wait at 10 seconds.</summary>
    /// <param name="seconds">The requested seconds.</param>
    /// <returns>The capped wait.</returns>
    public static TimeSpan Cap(double seconds)
    {
        if (seconds < 0)
        {
            return TimeSpan.Zero;
        }

        var requested = TimeSpan.FromSeconds(seconds);
        return requested > Maximum ? Maximum : requested;
    }
}
