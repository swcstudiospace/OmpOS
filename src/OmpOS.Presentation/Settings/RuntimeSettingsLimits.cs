using System.Globalization;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// The defaults, floors and ceilings of the Runtime settings section. The defaults (idle stop after 24 hours, completed-session
/// retention of 7 days) are the owner assumptions recorded in <c>.planning/STATE.md</c> line 124 (Plan Decisions and Assumptions,
/// 2026-10-22). STATE.md records no floor or ceiling, so the bounds below are conservative assumptions awaiting owner confirmation
/// (see the OQ row requested with this unit): idle stop 1 hour to 720 hours (30 days) and retention 1 day to 90 days.
/// </summary>
public static class RuntimeSettingsLimits
{
    /// <summary>The default idle stop window in hours (STATE.md line 124).</summary>
    public const int DefaultIdleStopHours = 24;

    /// <summary>The smallest accepted idle stop window in hours. A conservative assumption, not an owner decision.</summary>
    public const int MinIdleStopHours = 1;

    /// <summary>The largest accepted idle stop window in hours (30 days). A conservative assumption, not an owner decision.</summary>
    public const int MaxIdleStopHours = 720;

    /// <summary>The default completed-session retention in days (STATE.md line 124).</summary>
    public const int DefaultRetentionDays = 7;

    /// <summary>The smallest accepted retention in days. A conservative assumption, not an owner decision.</summary>
    public const int MinRetentionDays = 1;

    /// <summary>The largest accepted retention in days. A conservative assumption, not an owner decision.</summary>
    public const int MaxRetentionDays = 90;

    /// <summary>Validates an idle stop entry.</summary>
    /// <param name="text">The entered text, already trimmed. Null is treated as empty.</param>
    /// <param name="hours">The parsed hours when the entry is valid; otherwise zero.</param>
    /// <returns>The inline error message, or null when the entry is valid.</returns>
    public static string? ValidateIdleStop(string? text, out int hours) =>
        Validate(text, MinIdleStopHours, MaxIdleStopHours, "Idle stop", "hours", out hours);

    /// <summary>Validates a retention entry.</summary>
    /// <param name="text">The entered text, already trimmed. Null is treated as empty.</param>
    /// <param name="days">The parsed days when the entry is valid; otherwise zero.</param>
    /// <returns>The inline error message, or null when the entry is valid.</returns>
    public static string? ValidateRetention(string? text, out int days) =>
        Validate(text, MinRetentionDays, MaxRetentionDays, "Retention", "days", out days);

    /// <summary>Returns whether both values of <paramref name="values"/> are within their bounds.</summary>
    /// <param name="values">The values to check.</param>
    /// <returns>True when idle stop and retention are both in range.</returns>
    public static bool AreInRange(RuntimeSettingsValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IdleStopHours is >= MinIdleStopHours and <= MaxIdleStopHours
            && values.RetentionDays is >= MinRetentionDays and <= MaxRetentionDays;
    }

    private static string? Validate(string? text, int min, int max, string label, string unit, out int value)
    {
        value = 0;
        var entry = text?.Trim() ?? string.Empty;
        string rangeMessage = string.Create(
            CultureInfo.InvariantCulture,
            $"{label} must be a whole number of {unit} between {min} and {max}.");

        if (entry.Length == 0)
        {
            return rangeMessage;
        }

        if (!int.TryParse(entry, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsed)
            || parsed < min
            || parsed > max)
        {
            return rangeMessage;
        }

        value = parsed;
        return null;
    }
}
