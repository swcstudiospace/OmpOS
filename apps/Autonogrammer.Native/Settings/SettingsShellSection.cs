namespace Autonogrammer.Native.Settings;

/// <summary>
/// The shell members of <c>settings.json</c>. The surface value is <c>shell.surface</c>. The words <c>code</c> and <c>cowork</c> come from
/// <c>SurfaceSwitchModel</c>, not from this type.
/// </summary>
internal static class SettingsShellSection
{
    /// <summary>The JSON member that stores the selected surface.</summary>
    public const string SurfaceMember = "shell.surface";
}
