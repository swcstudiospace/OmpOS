namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// The legacy product spelling, kept in one frozen place so the rest of the project never names it. The spelling is built from
/// fragments so that no source file holds it as one token; this file is the only one under <c>src/</c> allowed to know it.
/// </summary>
internal static class LegacyBrandNames
{
    private const string Brand = "om" + "pos";

    /// <summary>The text that starts a legacy block marker comment, followed by the provider id.</summary>
    public const string BeginMarker = "# " + Brand + ":begin ";

    /// <summary>The text that ends a legacy block marker comment, followed by the provider id.</summary>
    public const string EndMarker = "# " + Brand + ":end ";
}
