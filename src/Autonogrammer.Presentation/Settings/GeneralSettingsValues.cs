namespace Autonogrammer.Presentation.Settings;

/// <summary>The saved values of the General settings page.</summary>
/// <param name="Motion">The motion setting a head applies to its shared controls.</param>
/// <param name="Theme">The colour theme a head applies to its window.</param>
public sealed record GeneralSettingsValues(MotionPreference Motion, ThemePreference Theme)
{
    /// <summary>Gets the page defaults: motion follows the operating system and the theme is light.</summary>
    public static GeneralSettingsValues Defaults { get; } = new(MotionPreference.System, ThemePreference.Light);
}
