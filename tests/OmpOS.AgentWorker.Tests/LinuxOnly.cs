namespace OmpOS.AgentWorker.Tests;

/// <summary>Skips tests whose fixtures are POSIX shell scripts when the run is not on Linux.</summary>
internal static class LinuxOnly
{
    /// <summary>Skips the current test unless the platform is Linux.</summary>
    public static void Require()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fixtures are POSIX shell scripts.");
        }
    }
}
