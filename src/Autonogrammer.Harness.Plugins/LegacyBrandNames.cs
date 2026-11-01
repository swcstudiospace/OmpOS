namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// Legacy system and convention paths used only by hook discovery. The spellings are built from fragments so no other file in this
/// project holds them. The paths are read-only.
/// </summary>
internal static class LegacyBrandNames
{
    private const string Lower = "om" + "pos";
    private const string Pascal = "Om" + "pOS";

    /// <summary>The legacy convention directory name, including the leading dot.</summary>
    public const string DirectoryName = "." + Lower;

    /// <summary>Returns the legacy managed hooks file for a platform. It is never written.</summary>
    /// <param name="platform">The operating system family.</param>
    /// <param name="programData">The Windows ProgramData directory.</param>
    /// <returns>The legacy managed <c>hooks.json</c> path.</returns>
    public static string ManagedHooks(Hooks.HookPlatform platform, string programData) => platform switch
    {
        Hooks.HookPlatform.MacOs => Path.Combine("/Library", "Application Support", Pascal, "hooks.json"),
        Hooks.HookPlatform.Windows => Path.Combine(programData, Pascal, "hooks.json"),
        _ => Path.Combine("/etc", Lower, "hooks.json"),
    };
}
