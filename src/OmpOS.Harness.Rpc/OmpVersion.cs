using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Rpc;

/// <summary>A parsed omp release version.</summary>
/// <param name="Major">The major version number.</param>
/// <param name="Minor">The minor version number.</param>
/// <param name="Patch">The patch version number.</param>
[StructLayout(LayoutKind.Auto)]
public readonly partial record struct OmpVersion(int Major, int Minor, int Patch)
{
    /// <summary>The major release line this client is pinned to.</summary>
    public const int PinnedMajor = 18;

    /// <summary>The minor release line this client is pinned to.</summary>
    public const int PinnedMinor = 12;

    /// <summary>Gets whether this version is on the pinned <c>18.12.x</c> line.</summary>
    public bool IsPinnedRelease => Major == PinnedMajor && Minor == PinnedMinor;

    /// <summary>Parses the first <c>major.minor.patch</c> triple in <paramref name="text"/>, such as <c>omp/18.12.3</c>.</summary>
    /// <param name="text">The output of <c>omp --version</c>.</param>
    /// <returns>The parsed version.</returns>
    /// <exception cref="ProcessStartHarnessException">The text contains no version triple.</exception>
    public static OmpVersion Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Match match = VersionPattern().Match(text);
        if (!match.Success)
        {
            throw new ProcessStartHarnessException($"Unrecognized omp version output: '{text.Trim()}'.");
        }

        return new OmpVersion(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    [GeneratedRegex(
        @"(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionPattern();
}
