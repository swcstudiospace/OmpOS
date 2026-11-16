using System.Text.RegularExpressions;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// The shapes a value must have before it is used in a remote script or believed from a probe: a plugin directory name, a commit and a plugin
/// source. Every such value that reaches a script passes one of these first, and the script quotes it as well.
/// </summary>
internal static partial class RemoteProvisionRules
{
    /// <summary>The source of a plugin that ships inside this checkout. The remote side cannot fetch it.</summary>
    public const string InRepoSource = "in-repo";

    /// <summary>Whether a plugin directory name is one path segment with no separator or dot-dot.</summary>
    public static bool IsValidDir(string? value) => value is not null && DirPattern().IsMatch(value);

    /// <summary>Whether a value is a full lowercase 40-character commit name.</summary>
    public static bool IsValidCommit(string? value) => value is not null && CommitPattern().IsMatch(value);

    /// <summary>Whether a source is <see cref="InRepoSource"/> or an https address of a git repository, with no whitespace or shell syntax.</summary>
    public static bool IsValidSource(string? value) => value is not null && (string.Equals(value, InRepoSource, StringComparison.Ordinal) || SourcePattern().IsMatch(value));

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DirPattern();

    [GeneratedRegex(@"^[0-9a-f]{40}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommitPattern();

    [GeneratedRegex(@"^https://[A-Za-z0-9.-]+/[A-Za-z0-9._~/-]{1,512}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SourcePattern();
}
