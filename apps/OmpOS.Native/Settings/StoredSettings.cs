using OmpOS.Presentation;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Settings;

/// <summary>The typed values of <c>settings.json</c>, with the defaults filled in for every member the file does not hold.</summary>
/// <param name="Motion">The motion setting of the shell.</param>
/// <param name="Theme">The colour theme of the window.</param>
/// <param name="TerminalAutoRun">True when proposed terminal commands run without waiting for an approval.</param>
/// <param name="Runtime">The idle stop and retention windows.</param>
internal sealed record StoredSettings(MotionPreference Motion, ThemePreference Theme, bool TerminalAutoRun, RuntimeSettingsValues Runtime)
{
    /// <summary>Gets the defaults: motion follows the system, the theme is dark, every terminal command waits for an approval, 24 hours idle stop and 7 days retention.</summary>
    public static StoredSettings Defaults { get; } = new(MotionPreference.System, ThemePreference.Dark, TerminalAutoRun: false, RuntimeSettingsValues.Defaults);
}
