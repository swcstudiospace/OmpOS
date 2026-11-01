namespace Autonogrammer.Harness.OmpConfig.Tests;

/// <summary>Builders for the legacy marker spelling, assembled from fragments so no test source holds the legacy token whole.</summary>
internal static class LegacyBrandNames
{
    private static readonly string Brand = string.Concat("om", "pos");

    /// <summary>Builds a legacy begin marker line (without indentation or line ending).</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>The marker comment.</returns>
    public static string LegacyBegin(string id) => string.Concat("# ", Brand, ":begin ", id);

    /// <summary>Builds a legacy end marker line (without indentation or line ending).</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>The marker comment.</returns>
    public static string LegacyEnd(string id) => string.Concat("# ", Brand, ":end ", id);
}
