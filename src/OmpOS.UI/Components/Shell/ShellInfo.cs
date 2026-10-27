using System.Globalization;
using System.Reflection;

namespace OmpOS.UI.Components.Shell;

/// <summary>
/// Facts the shell shows on every screen: where the source lives, the licence it is under, and the running version. The AGPL
/// section 13 source offer is these three, rendered by the footer.
/// </summary>
public static class ShellInfo
{
    /// <summary>The number of leading characters of the build metadata (the commit) the footer keeps.</summary>
    private const int MetadataLength = 7;

    /// <summary>The URL of the source repository.</summary>
    public const string SourceRepositoryUrl = "https://github.com/swcstudiospace/OmpOS";

    /// <summary>The SPDX identifier of the licence.</summary>
    public const string LicenceId = "AGPL-3.0-or-later";

    /// <summary>The URL of the licence text.</summary>
    public const string LicenceUrl = "https://www.gnu.org/licenses/agpl-3.0.html";

    /// <summary>Gets the shown version of the running UI assembly (its informational version, without a long commit suffix).</summary>
    public static string CurrentVersion { get; } = FormatVersion(
        typeof(ShellInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>Formats an informational version for display: the version, plus the first seven characters of any build metadata.</summary>
    /// <param name="informationalVersion">The informational version, for example <c>0.15.0+0123456789abcdef</c>, or <c>null</c>.</param>
    /// <returns>The version to show, for example <c>0.15.0+0123456</c>; <c>dev</c> when there is no version.</returns>
    public static string FormatVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "dev";
        }

        var trimmed = informationalVersion.Trim();
        var plus = trimmed.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
        {
            return trimmed;
        }

        var version = trimmed[..plus];
        var metadata = trimmed[(plus + 1)..];
        if (metadata.Length == 0)
        {
            return version;
        }

        var kept = metadata.Length <= MetadataLength ? metadata : metadata[..MetadataLength];
        return string.Create(CultureInfo.InvariantCulture, $"{version}+{kept}");
    }
}
