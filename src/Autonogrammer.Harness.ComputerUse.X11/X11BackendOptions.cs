using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.X11;

/// <summary>How the X11 backend opens its display. Tests pass <see cref="DisplayName"/> and never touch the session.</summary>
public sealed class X11BackendOptions
{
    /// <summary>
    /// Display name passed to <c>XOpenDisplay</c>. <see langword="null"/> reads <c>DISPLAY</c> through <see cref="Environment"/>.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Head process environment reader. Defaults to the process environment. Not the engine allow-list.
    /// </summary>
    public Func<string, string?> Environment { get; init; } = static name => System.Environment.GetEnvironmentVariable(name);

    /// <summary>Head-provided clipboard. The backend implements no X11 clipboard protocol.</summary>
    public IClipboardTextPort? Clipboard { get; init; }

    /// <summary>Clock for restore logs. Defaults to <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>Receives fixed failure sentences. Never a window title or typed text.</summary>
    public Action<string>? Log { get; init; }


    /// <summary>Display passed to <c>XOpenDisplay</c>. An explicit <see cref="DisplayName"/> wins over the environment.</summary>
    /// <returns>The display name, or <see langword="null"/> when none is configured.</returns>
    public string? ResolveDisplayName()
    {
        if (!string.IsNullOrEmpty(DisplayName))
        {
            return DisplayName;
        }

        return Environment("DISPLAY");
    }

    /// <summary>
    /// Environment reader for availability. An explicit <see cref="DisplayName"/> is reported as <c>DISPLAY</c>
    /// so a private Xvfb is not classified from the session display.
    /// </summary>
    /// <returns>The reader.</returns>
    public Func<string, string?> SessionReader()
    {
        return name => string.Equals(name, "DISPLAY", StringComparison.Ordinal) && !string.IsNullOrEmpty(DisplayName)
            ? DisplayName
            : Environment(name);
    }
}