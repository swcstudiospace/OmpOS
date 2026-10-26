namespace OmpOS.Presentation.Settings;

/// <summary>The saved values of the General settings page.</summary>
/// <param name="Motion">The motion setting a head applies to its shared controls.</param>
public sealed record GeneralSettingsValues(MotionPreference Motion)
{
    /// <summary>Gets the page defaults: motion follows the operating system.</summary>
    public static GeneralSettingsValues Defaults { get; } = new(MotionPreference.System);
}
