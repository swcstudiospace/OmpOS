namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>
/// Builds the Chromium child environment from the D-07 allow-list only. This is not the engine policy: proxy, CA, omp and
/// coding-agent variables are dropped, as is every other name.
/// </summary>
public static class BrowserEnvironment
{
    private static readonly string[] ExactNames =
    [
        "PATH",
        "HOME",
        "USERPROFILE",
        "TMPDIR",
        "TEMP",
        "LANG",
        "DISPLAY",
        "WAYLAND_DISPLAY",
        "XDG_RUNTIME_DIR",
        "SystemRoot",
        "LOCALAPPDATA",
        "APPDATA",
    ];

    /// <summary>
    /// Returns the variables a Chromium child may inherit. Secret-shaped names are dropped even when a prefix would otherwise keep them.
    /// </summary>
    /// <param name="parent">The parent environment. Null values are skipped.</param>
    /// <returns>The child block, in the parent's order.</returns>
    public static IReadOnlyDictionary<string, string> Build(IEnumerable<KeyValuePair<string, string?>> parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        var child = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var pair in parent)
        {
            if (pair.Value is null || !IsAllowed(pair.Key))
            {
                continue;
            }

            child[pair.Key] = pair.Value;
        }

        return child;
    }

    /// <summary>Returns whether <paramref name="name"/> is on the browser allow-list and is not secret-shaped.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True when the child may inherit it.</returns>
    public static bool IsAllowed(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (IsSecretShaped(name))
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (name.StartsWith("LC_", comparison))
        {
            return true;
        }

        foreach (var allowed in ExactNames)
        {
            if (string.Equals(name, allowed, comparison))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSecretShaped(string name)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (name.StartsWith("OMP_", comparison) || name.StartsWith("AUTONOGRAMMER_", comparison))
        {
            return true;
        }

        return name.EndsWith("_KEY", comparison)
            || name.EndsWith("_TOKEN", comparison)
            || name.EndsWith("_SECRET", comparison);
    }
}
