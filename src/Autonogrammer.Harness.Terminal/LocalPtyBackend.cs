namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// Selects the local pseudo-terminal backend for the operating system this process is running on.
/// </summary>
public static class LocalPtyBackend
{
    /// <summary>
    /// Returns the backend for this process. Windows inherits the process environment and applies the caller's
    /// overrides (OQ-516). Linux keeps the allowlist. macOS has no local terminal backend.
    /// </summary>
    /// <returns>The backend the desktop head uses when the caller does not supply one.</returns>
    /// <exception cref="PlatformNotSupportedException">The operating system is neither Windows nor Linux.</exception>
    public static ITerminalBackend ForCurrentOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsLocalPtyBackend();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxLocalPtyBackend();
        }

        throw new PlatformNotSupportedException(
            "The local terminal backend runs on Windows and Linux. macOS has no local terminal backend yet.");
    }
}
