using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace OmpOS.Harness.Automations.Cron;

/// <summary>
/// A five-field cron expression (minute, hour, day of month, month, day of week) with the next-occurrence calculation the
/// automation scheduler needs. Fields accept <c>*</c>, single values, ranges (<c>9-17</c>), lists (<c>1,15</c>), steps
/// (<c>*/15</c>, <c>0-30/10</c>, <c>5/20</c>) and, for the month and day-of-week fields, three-letter names (<c>jan</c>, <c>mon-fri</c>).
/// Seconds, <c>?</c>, <c>L</c>, <c>W</c> and <c>#</c> are not supported. The presets <c>@yearly</c>, <c>@annually</c>, <c>@monthly</c>,
/// <c>@weekly</c>, <c>@daily</c>, <c>@midnight</c> and <c>@hourly</c> are accepted.
/// </summary>
/// <remarks>
/// <para>
/// Day rule (the classic cron rule): when both the day-of-month and the day-of-week field are restricted (neither starts with
/// <c>*</c>), a day matches when either field matches; otherwise both must match.
/// </para>
/// <para>
/// Time zones: the expression is evaluated against the wall clock of a <see cref="TimeZoneInfo"/>. A local time that does not
/// exist because clocks went forward fires once, at the instant the clock changes; several skipped times collapse into that one
/// instant. A local time that happens twice because clocks went back fires once, at its first occurrence. When the hour field is a
/// wildcard (<c>*</c> or <c>*/n</c>) both rules change, as in classic cron: skipped times do not fire at all, and a repeated time fires
/// at both occurrences, so an hourly or half-hourly job keeps its rhythm through a clock change.
/// </para>
/// </remarks>
public sealed class CronExpression
{
    /// <summary>The longest expression text accepted.</summary>
    public const int MaxLength = 256;

    /// <summary>The most occurrences <see cref="GetNextOccurrences"/> returns in one call.</summary>
    public const int MaxOccurrences = 1000;

    private const int YearsAhead = 9;
    private static readonly TimeSpan OffsetProbe = TimeSpan.FromHours(26);
    private static readonly char[] FieldSeparators = [' ', '\t'];

    private readonly CronField _minutes;
    private readonly CronField _hours;
    private readonly CronField _daysOfMonth;
    private readonly CronField _months;
    private readonly CronField _daysOfWeek;

    private CronExpression(string source, CronField minutes, CronField hours, CronField daysOfMonth, CronField months, CronField daysOfWeek)
    {
        Source = source;
        _minutes = minutes;
        _hours = hours;
        _daysOfMonth = daysOfMonth;
        _months = months;
        _daysOfWeek = daysOfWeek;
    }

    /// <summary>Gets the expression text as it was given, trimmed.</summary>
    public string Source { get; }

    /// <summary>Parses an expression.</summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expression">The expression when the call succeeds.</param>
    /// <param name="error">The reason when the call fails. It never repeats the text.</param>
    /// <returns>True when the text is a valid expression.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out CronExpression? expression, out string error)
    {
        expression = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "The cron expression is empty.";
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > MaxLength)
        {
            error = $"The cron expression is longer than {MaxLength.ToString(CultureInfo.InvariantCulture)} characters.";
            return false;
        }

        var expanded = Expand(trimmed);
        var parts = expanded.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
        {
            error = parts.Length == 6
                ? "A cron expression has five fields (minute hour day-of-month month day-of-week). Seconds are not supported."
                : "A cron expression has five fields: minute hour day-of-month month day-of-week.";
            return false;
        }

        if (!CronField.TryParse(parts[0], CronFieldKind.Minute, out var minutes, out error)
            || !CronField.TryParse(parts[1], CronFieldKind.Hour, out var hours, out error)
            || !CronField.TryParse(parts[2], CronFieldKind.DayOfMonth, out var daysOfMonth, out error)
            || !CronField.TryParse(parts[3], CronFieldKind.Month, out var months, out error)
            || !CronField.TryParse(parts[4], CronFieldKind.DayOfWeek, out var daysOfWeek, out error))
        {
            return false;
        }

        expression = new CronExpression(trimmed, minutes, hours, daysOfMonth, months, daysOfWeek);
        error = string.Empty;
        return true;
    }

    /// <summary>Parses an expression.</summary>
    /// <param name="text">The expression text.</param>
    /// <returns>The expression.</returns>
    /// <exception cref="FormatException">The text is not a valid expression.</exception>
    public static CronExpression Parse(string text) =>
        TryParse(text, out var expression, out var error) ? expression : throw new FormatException(error);

    /// <summary>Returns the first occurrence strictly after an instant.</summary>
    /// <param name="after">The instant to start after.</param>
    /// <param name="zone">The time zone whose wall clock the expression is evaluated against.</param>
    /// <returns>The occurrence, in UTC, or null when none exists within nine years (for example 30 February).</returns>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset after, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var afterUtc = after.ToUniversalTime();
        var localAfter = TimeZoneInfo.ConvertTime(afterUtc, zone).DateTime;

        // When `after` falls in the first pass of a repeated hour, local times before it can still have a later second pass.
        var stepBack = zone.GetUtcOffset(afterUtc) - zone.GetUtcOffset(afterUtc + OffsetProbe);
        if (stepBack < TimeSpan.Zero)
        {
            stepBack = TimeSpan.Zero;
        }

        var start = TruncateToMinute(localAfter - stepBack);
        var candidate = NextLocal(start);
        DateTimeOffset? best = null;
        var bestLocal = DateTime.MinValue;
        var reach = TimeSpan.Zero;
        while (candidate is { } local)
        {
            if (best is not null && local > bestLocal + reach)
            {
                break;
            }

            var count = CronLocalTime.Map(local, zone, _hours.IsWildcard, out var first, out var second);
            for (var index = 0; index < count; index++)
            {
                var instant = index == 0 ? first : second;
                if (instant > afterUtc && (best is null || instant < best.Value))
                {
                    best = instant;
                    bestLocal = TimeZoneInfo.ConvertTime(instant, zone).DateTime;
                    var shift = zone.GetUtcOffset(instant + OffsetProbe) - zone.GetUtcOffset(instant - OffsetProbe);
                    reach = shift < TimeSpan.Zero ? -shift : shift;
                }
            }

            candidate = local.Year >= 9998 ? null : NextLocal(local.AddMinutes(1));
        }

        return best;
    }

    /// <summary>Returns the next occurrences after an instant, each strictly after the one before.</summary>
    /// <param name="after">The instant to start after.</param>
    /// <param name="zone">The time zone whose wall clock the expression is evaluated against.</param>
    /// <param name="count">How many occurrences to return, 1 to <see cref="MaxOccurrences"/>.</param>
    /// <returns>Up to <paramref name="count"/> instants in UTC, fewer when the expression runs out of occurrences.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is outside 1 to <see cref="MaxOccurrences"/>.</exception>
    public IReadOnlyList<DateTimeOffset> GetNextOccurrences(DateTimeOffset after, TimeZoneInfo zone, int count)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxOccurrences);
        var result = new List<DateTimeOffset>(count);
        var cursor = after;
        while (result.Count < count && GetNextOccurrence(cursor, zone) is { } next)
        {
            result.Add(next);
            cursor = next;
        }

        return result;
    }

    private static string Expand(string text) => text switch
    {
        "@yearly" or "@annually" => "0 0 1 1 *",
        "@monthly" => "0 0 1 * *",
        "@weekly" => "0 0 * * 0",
        "@daily" or "@midnight" => "0 0 * * *",
        "@hourly" => "0 * * * *",
        _ => text,
    };

    private static DateTime TruncateToMinute(DateTime value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMinute), DateTimeKind.Unspecified);

    private bool DayMatches(DateTime value)
    {
        var inMonth = _daysOfMonth.Contains(value.Day);
        var inWeek = _daysOfWeek.Contains((int)value.DayOfWeek);
        return _daysOfMonth.IsWildcard || _daysOfWeek.IsWildcard ? inMonth && inWeek : inMonth || inWeek;
    }

    /// <summary>Finds the first wall-clock time at or after <paramref name="from"/> that the fields allow.</summary>
    private DateTime? NextLocal(DateTime from)
    {
        var limitYear = Math.Min(from.Year + YearsAhead, 9998);
        var time = from;
        while (time.Year <= limitYear)
        {
            if (!_months.Contains(time.Month))
            {
                time = new DateTime(time.Year, time.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1);
                continue;
            }

            if (!DayMatches(time))
            {
                time = time.Date.AddDays(1);
                continue;
            }

            if (!_hours.Contains(time.Hour))
            {
                time = time.Date.AddHours(time.Hour + 1);
                continue;
            }

            var minute = _minutes.NextAtLeast(time.Minute);
            if (minute < 0)
            {
                time = time.Date.AddHours(time.Hour + 1);
                continue;
            }

            return new DateTime(time.Year, time.Month, time.Day, time.Hour, minute, 0, DateTimeKind.Unspecified);
        }

        return null;
    }
}
