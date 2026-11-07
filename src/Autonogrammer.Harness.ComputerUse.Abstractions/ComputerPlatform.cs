namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Operating-system session the Computer Use backend speaks.</summary>
public enum ComputerPlatform
{
    /// <summary>X11 (Xlib / XTest). Wayland is not this value; it is <see cref="ComputerAvailability.WaylandUnsupported"/>.</summary>
    X11 = 0,

    /// <summary>Win32 desktop session.</summary>
    Windows = 1,

    /// <summary>macOS Quartz session.</summary>
    MacOS = 2,

    /// <summary>No supported desktop session. Tools are not registered.</summary>
    Unsupported = 3,
}

