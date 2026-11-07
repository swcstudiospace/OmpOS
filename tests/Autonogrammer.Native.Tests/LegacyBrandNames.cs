namespace Autonogrammer.Native.Tests;

/// <summary>
/// The legacy product spelling for the migration fixtures, built from fragments so that no source file holds it as one token. This file
/// is the only one in this test project allowed to know it.
/// </summary>
internal static class LegacyBrandNames
{
    /// <summary>The name of the per-user data directory the previous product name used.</summary>
    public const string UserDataDirectory = "Om" + "pOS";

    /// <summary>The legacy convention directory name, including the leading dot.</summary>
    public const string ConventionDirectory = "." + "om" + "pos";
}
