namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Names and parsing of <see cref="RunMode"/> values. The wire names are the ones Cursor's CLI uses for <c>approvalMode</c>.</summary>
public static class RunModes
{
    /// <summary>Gets the mode used when the user has chosen none: <see cref="RunMode.AutoReview"/>, as in Cursor.</summary>
    public static RunMode Default => RunMode.AutoReview;

    /// <summary>Returns the name a mode has in files: <c>auto-review</c>, <c>allowlist</c> or <c>unrestricted</c>.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The wire name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined value.</exception>
    public static string WireName(RunMode mode) => mode switch
    {
        RunMode.AutoReview => "auto-review",
        RunMode.Allowlist => "allowlist",
        RunMode.RunEverything => "unrestricted",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown run mode."),
    };

    /// <summary>Parses a wire name. It also accepts <c>run-everything</c>, and ignores case, spaces and underscores.</summary>
    /// <param name="text">The text, or null.</param>
    /// <param name="mode">The parsed mode, or <see cref="Default"/> when the text names none.</param>
    /// <returns>True when the text names a mode.</returns>
    public static bool TryParse(string? text, out RunMode mode)
    {
        mode = Default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var key = text.Trim().Replace('_', '-').Replace(' ', '-').ToUpperInvariant();
        switch (key)
        {
            case "AUTO-REVIEW" or "AUTOREVIEW":
                mode = RunMode.AutoReview;
                return true;
            case "ALLOWLIST":
                mode = RunMode.Allowlist;
                return true;
            case "UNRESTRICTED" or "RUN-EVERYTHING" or "RUNEVERYTHING":
                mode = RunMode.RunEverything;
                return true;
            default:
                return false;
        }
    }
}
