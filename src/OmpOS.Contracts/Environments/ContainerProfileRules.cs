using System.Text.RegularExpressions;

namespace OmpOS.Contracts.Environments;

/// <summary>
/// The rules a <see cref="ContainerProfile"/> and a <see cref="WslProfile"/> must obey before a value reaches a process argument or a shell line inside the
/// container or distribution. The values travel as separate process arguments (never through a local shell), and the dynamic parts of the remote script are
/// single-quoted; the rules here still refuse anything that could be read as an option or that holds a control character.
/// </summary>
public static partial class ContainerProfileRules
{
    /// <summary>
    /// Returns whether <paramref name="value"/> is a usable container reference: a container name or an id of 1 to 128 letters, digits, <c>.</c>, <c>_</c> or
    /// <c>-</c> that starts with a letter or digit (podman's own rule), so it can never be read as an option.
    /// </summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidContainerReference(string? value) => value is not null && ContainerReferencePattern().IsMatch(value);

    /// <summary>Returns whether <paramref name="value"/> is a usable WSL distribution name: 1 to 64 letters, digits, <c>.</c>, <c>_</c> or <c>-</c>, starting with a letter or digit.</summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidWslDistribution(string? value) => value is not null && DistributionPattern().IsMatch(value);

    /// <summary>Returns whether <paramref name="value"/> is a usable user inside a container or distribution: 1 to 64 letters, digits, <c>.</c>, <c>_</c> or <c>-</c> (a numeric id passes), not starting with <c>-</c>.</summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidInnerUser(string? value) => SshProfileRules.IsValidUser(value);

    /// <summary>
    /// Returns whether <paramref name="value"/> is a usable podman executable on this machine: the bare name <c>podman</c> (or another plain command name), or a
    /// fully qualified path without control characters that does not start with <c>-</c>.
    /// </summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidPodmanPath(string? value) =>
        value is not null && (ExecutableNamePattern().IsMatch(value) || (SshProfileRules.IsValidLocalPath(value) && Path.IsPathFullyQualified(value)));

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ContainerReferencePattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DistributionPattern();

    [GeneratedRegex("^[A-Za-z0-9._+][A-Za-z0-9._+-]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExecutableNamePattern();
}
