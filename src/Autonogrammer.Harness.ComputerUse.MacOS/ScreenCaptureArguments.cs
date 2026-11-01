using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.MacOS;

/// <summary>
/// Pure argument vector for <c>/usr/sbin/screencapture</c>. No shell string and no process start.
/// screencapture attribution is unverified until a Mac run (OQ-382). Do not fall back to another capture mechanism.
/// </summary>
public static class ScreenCaptureArguments
{
    /// <summary>Absolute path. The runner must not search <c>PATH</c>.</summary>
    public const string Executable = "/usr/sbin/screencapture";

    /// <summary>Capture timeout. The runner kills the child when this elapses.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Builds <c>["-x", "-t", "png", outfile]</c>. <paramref name="fileName"/> is a single segment under
    /// <paramref name="stateDirectory"/>.
    /// </summary>
    /// <param name="stateDirectory">State directory that owns the outfile.</param>
    /// <param name="fileName">File name only. No separators.</param>
    /// <returns>The argument vector. Index 3 is the outfile.</returns>
    public static IReadOnlyList<string> Create(string stateDirectory, string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(stateDirectory);
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        if (fileName.Contains('/', StringComparison.Ordinal)
            || fileName.Contains('\\', StringComparison.Ordinal)
            || fileName.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Capture file name must be a single path segment.", nameof(fileName));
        }

        string outfile = Path.Combine(stateDirectory, fileName);
        return ["-x", "-t", "png", outfile];
    }

    /// <summary>
    /// Keeps only <c>HOME</c>, <c>TMPDIR</c>, <c>USER</c>, <c>LOGNAME</c>, <c>LANG</c> and names starting with
    /// <c>LC_</c>. Every other variable is dropped, including proxy and provider keys.
    /// </summary>
    /// <param name="source">The parent environment. Not mutated.</param>
    /// <returns>The allow-listed environment.</returns>
    public static IReadOnlyDictionary<string, string> ScreenCaptureEnvironment(IReadOnlyDictionary<string, string> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in source)
        {
            if (IsAllowed(pair.Key))
            {
                kept[pair.Key] = pair.Value;
            }
        }

        return kept;
    }

    /// <summary>
    /// Points to capture pixels through <see cref="CoordinateMap"/> only. Scale is applied once:
    /// points times <see cref="DisplayInfo.ScaleFactor"/>, plus the display origin.
    /// </summary>
    /// <param name="display">The display. Its scale is the points-to-pixels ratio.</param>
    /// <param name="points">Point relative to the display origin.</param>
    /// <returns>Capture pixels.</returns>
    public static NativePoint ToCapturePixels(DisplayInfo display, ModelPoint points)
    {
        return CoordinateMap.ScaleToNative(display, points);
    }

    private static bool IsAllowed(string name)
    {
        return name is "HOME" or "TMPDIR" or "USER" or "LOGNAME" or "LANG"
            || name.StartsWith("LC_", StringComparison.Ordinal);
    }
}
