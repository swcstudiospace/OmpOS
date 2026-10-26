namespace OmpOS.Presentation.Settings;

/// <summary>The saved values of the Runtime settings section.</summary>
/// <param name="IdleStopHours">Whole hours of inactivity after which a session sleeps. Within <see cref="RuntimeSettingsLimits"/>.</param>
/// <param name="RetentionDays">Whole days a completed session is kept before it is removed. Within <see cref="RuntimeSettingsLimits"/>.</param>
public sealed record RuntimeSettingsValues(int IdleStopHours, int RetentionDays)
{
    /// <summary>Gets the section defaults: idle stop after 24 hours and completed-session retention of 7 days.</summary>
    public static RuntimeSettingsValues Defaults { get; } =
        new(RuntimeSettingsLimits.DefaultIdleStopHours, RuntimeSettingsLimits.DefaultRetentionDays);

    /// <summary>Gets the idle stop window as a duration.</summary>
    public TimeSpan IdleStop => TimeSpan.FromHours(IdleStopHours);

    /// <summary>Gets the completed-session retention window as a duration.</summary>
    public TimeSpan Retention => TimeSpan.FromDays(RetentionDays);
}
