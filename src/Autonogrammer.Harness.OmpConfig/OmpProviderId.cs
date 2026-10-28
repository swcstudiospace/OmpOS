using System.Text.RegularExpressions;

namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// The shape of a provider id this library accepts anywhere it reaches omp: a lower-case slug of at most 64 characters. The
/// check runs before any process starts or file is touched.
/// </summary>
public static partial class OmpProviderId
{
    /// <summary>The longest provider id accepted.</summary>
    public const int MaxLength = 64;

    /// <summary>Tells whether <paramref name="providerId"/> matches <c>[a-z0-9][a-z0-9._-]{0,63}</c>.</summary>
    /// <param name="providerId">The id to check; null is not valid.</param>
    /// <returns>True when the id is acceptable.</returns>
    public static bool IsValid(string? providerId) =>
        providerId is not null && Pattern().IsMatch(providerId);

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();
}
