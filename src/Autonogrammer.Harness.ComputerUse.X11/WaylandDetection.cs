using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.X11;

/// <summary>
/// Pure session classification. A session is Wayland when <c>XDG_SESSION_TYPE</c> is wayland or
/// <c>WAYLAND_DISPLAY</c> is set, even if <c>DISPLAY</c> points at XWayland.
/// </summary>
public static class WaylandDetection
{
    /// <summary>Guidance shown for <see cref="ComputerAvailability.WaylandUnsupported"/>.</summary>
    public const string WaylandMessage = "Computer Use needs an X11 session. You are signed in to a Wayland session. Sign out and choose the Xorg (X11) session on the login screen.";

    /// <summary>Reads session variables only through <paramref name="readEnvironment"/>.</summary>
    /// <param name="readEnvironment">Head process environment. Not the engine allow-list.</param>
    /// <returns><see langword="true"/> when the session is Wayland, including XWayland.</returns>
    public static bool IsWayland(Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        string? session = readEnvironment("XDG_SESSION_TYPE");
        if (string.Equals(session, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrEmpty(readEnvironment("WAYLAND_DISPLAY"));
    }

    /// <summary>
    /// Maps the environment and an <c>XOpenDisplay</c> result to an availability state.
    /// Wayland wins over a successful open. An unset <c>DISPLAY</c> or a failed open is <see cref="ComputerAvailability.NoDisplay"/>.
    /// </summary>
    /// <param name="readEnvironment">Head process environment.</param>
    /// <param name="displayOpenSucceeded"><see langword="true"/> when <c>XOpenDisplay</c> returned a display.</param>
    /// <returns>The availability state. The remediation id is <see cref="ComputerAvailabilityRemediation.For"/>.</returns>
    public static ComputerAvailability Classify(Func<string, string?> readEnvironment, bool displayOpenSucceeded)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        if (IsWayland(readEnvironment))
        {
            return ComputerAvailability.WaylandUnsupported;
        }

        string? display = readEnvironment("DISPLAY");
        if (string.IsNullOrEmpty(display) || !displayOpenSucceeded)
        {
            return ComputerAvailability.NoDisplay;
        }

        return ComputerAvailability.Available;
    }
}
