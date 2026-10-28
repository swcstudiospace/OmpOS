namespace Autonogrammer.Workspace.Tests;

/// <summary>
/// Checks that a host executable used by a test exists. A missing executable fails the test with a clear message;
/// the test is never skipped.
/// </summary>
internal static class HostCommand
{
    /// <summary>
    /// Returns the absolute path of a host executable.
    /// </summary>
    /// <param name="absolutePath">The executable path, for example <c>/bin/sh</c>.</param>
    /// <exception cref="InvalidOperationException">The executable is absent on this host.</exception>
    public static string Require(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            throw new InvalidOperationException(
                $"Required host command '{absolutePath}' is absent on this host; the setup runner tests cannot run.");
        }

        return absolutePath;
    }
}
