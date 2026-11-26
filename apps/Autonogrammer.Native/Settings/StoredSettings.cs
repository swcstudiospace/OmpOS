using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Settings;

/// <summary>The typed values of <c>settings.json</c>, with the defaults filled in for every member the file does not hold.</summary>
/// <param name="Motion">The motion setting of the shell.</param>
/// <param name="Theme">The colour theme of the window.</param>
/// <param name="Runtime">The idle stop and retention windows.</param>
internal sealed record StoredSettings(MotionPreference Motion, ThemePreference Theme, RuntimeSettingsValues Runtime)
{
    /// <summary>Gets the defaults: motion follows the system, the theme is light, 24 hours idle stop and 7 days retention.</summary>
    public static StoredSettings Defaults { get; } = new(MotionPreference.System, ThemePreference.Light, RuntimeSettingsValues.Defaults);

    /// <summary>Gets the stored surface text, or null when the file has no surface member.</summary>
    public string? Surface { get; init; }
}
