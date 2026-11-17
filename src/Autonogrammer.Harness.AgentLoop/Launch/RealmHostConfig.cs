namespace Autonogrammer.Harness.AgentLoop.Launch;

/// <summary>
/// Makes the host's Realm configuration visible to an isolated omp home. Realm reads <c>realm/config.json</c> from the first
/// absolute value of <c>XDG_CONFIG_HOME</c>, else from <c>HOME/.config</c>. The isolated profile points both at the sandbox, so
/// without a copy the Realm extension cannot load there and registers none of its tools. Sharing this one file is the owner's
/// decision for isolated sessions; nothing else of the host is shared.
/// </summary>
public static class RealmHostConfig
{
    /// <summary>The config file below a config root.</summary>
    public const string ConfigFileRelativePath = "realm/config.json";

    /// <summary>
    /// Finds the host's Realm config file the way Realm resolves it.
    /// </summary>
    /// <param name="variable">Reads one environment variable; returns null when it is unset.</param>
    /// <returns>The full path of the config file, or null when no config file exists there.</returns>
    public static string? ResolveHostPath(Func<string, string?> variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        var xdg = AbsoluteOrNull(variable("XDG_CONFIG_HOME"));
        var root = xdg ?? (AbsoluteOrNull(variable("HOME")) is { } home ? Path.Combine(home, ".config") : null);
        if (root is null)
        {
            return null;
        }

        var path = Path.Combine(root, ConfigFileRelativePath);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Copies the host config into <c>&lt;isolatedHome&gt;/home/.config/realm/config.json</c>, the path the isolated profile resolves.
    /// On platforms that support it, the copy is readable and writable by the owner only.
    /// </summary>
    /// <param name="isolatedHome">The isolated omp home: the sandbox directory the isolated profile was planned with.</param>
    /// <param name="hostConfigPath">The host config file, as returned by <see cref="ResolveHostPath"/>.</param>
    /// <returns>True when the file was copied; false when the host file does not exist, and nothing is created.</returns>
    public static bool ShareInto(string isolatedHome, string hostConfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isolatedHome);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostConfigPath);
        if (!File.Exists(hostConfigPath))
        {
            return false;
        }

        var directory = Path.Combine(isolatedHome, "home", ".config", "realm");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "config.json");
        File.Copy(hostConfigPath, destination, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(destination, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return true;
    }

    private static string? AbsoluteOrNull(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value) ? value : null;
}
