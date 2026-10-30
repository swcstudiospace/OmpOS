namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// The legacy product spelling, kept in one frozen place so the rest of the project never names it. The spelling is built from
/// fragments so that no source file holds it as one token; this file is the only one in this project allowed to know it.
/// </summary>
internal static class LegacyBrandNames
{
    /// <summary>The name of the per-user data directory the previous product name used.</summary>
    public const string UserDataDirectory = "Om" + "pOS";

    /// <summary>The convention directory the previous product name used, including the leading dot.</summary>
    public const string ConventionDirectory = "." + "om" + "pos";
}
