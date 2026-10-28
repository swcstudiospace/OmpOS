using System.Reflection;
using System.Text.RegularExpressions;
using OmpOS.Contracts.Catalog;

namespace OmpOS.Api.Services;

/// <summary>Builds the answer of <c>GET /source</c>: the AGPL-3.0 section 13 source offer for this build (ADR-0018).</summary>
internal sealed partial class SourceInfoProvider
{
    /// <summary>The SPDX identifier of the licence.</summary>
    public const string LicenceId = "AGPL-3.0-or-later";

    /// <summary>The address of the licence text.</summary>
    public const string LicenceAddress = "https://www.gnu.org/licenses/agpl-3.0.html";

    /// <summary>The address of the source repository.</summary>
    public const string SourceAddress = "https://github.com/swcstudiospace/OmpOS";

    /// <summary>The configuration key (environment variable) that names the revision when the assembly does not.</summary>
    public const string RevisionKey = "GIT_SHA";

    private const string Unknown = "unknown";

    private readonly SourceInfo _info;

    /// <summary>Initializes a new instance of the <see cref="SourceInfoProvider"/> class.</summary>
    /// <param name="configuration">The configuration, which supplies <c>GIT_SHA</c> from the environment.</param>
    public SourceInfoProvider(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var assembly = typeof(SourceInfoProvider).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        _info = new SourceInfo
        {
            Licence = LicenceId,
            LicenceUrl = new Uri(LicenceAddress),
            SourceUrl = new Uri(SourceAddress),
            Revision = RevisionOf(informational, configuration[RevisionKey]),
            Version = assembly.GetName().Version?.ToString() ?? Unknown,
        };
    }

    /// <summary>Gets the source offer.</summary>
    public SourceInfo Info => _info;

    /// <summary>Picks the revision: the build metadata of the informational version, else the environment value, else <c>unknown</c>. Only a hexadecimal object name of 7 to 64 digits is accepted.</summary>
    /// <param name="informationalVersion">The assembly informational version, for example <c>0.15.0+0123abc</c>, or null.</param>
    /// <param name="environmentValue">The <c>GIT_SHA</c> value, or null.</param>
    /// <returns>The revision, lower-case.</returns>
    public static string RevisionOf(string? informationalVersion, string? environmentValue)
    {
        var plus = informationalVersion?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        if (plus >= 0 && informationalVersion is not null)
        {
            var metadata = informationalVersion[(plus + 1)..];
            var dot = metadata.IndexOf('.', StringComparison.Ordinal);
            var candidate = dot >= 0 ? metadata[..dot] : metadata;
            if (ObjectName().IsMatch(candidate))
            {
                return LowerHex(candidate);
            }
        }

        return environmentValue is not null && ObjectName().IsMatch(environmentValue.Trim())
            ? LowerHex(environmentValue.Trim())
            : Unknown;
    }

    // Only hexadecimal digits reach this point, so setting bit 0x20 turns A-F into a-f and leaves 0-9 and a-f alone.
    private static string LowerHex(string hex) => string.Create(hex.Length, hex, static (span, source) =>
    {
        for (var i = 0; i < source.Length; i++)
        {
            span[i] = (char)(source[i] | 0x20);
        }
    });

    [GeneratedRegex("^[0-9a-fA-F]{7,64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ObjectName();
}
