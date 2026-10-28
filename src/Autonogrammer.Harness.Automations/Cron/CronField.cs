using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace Autonogrammer.Harness.Automations.Cron;

/// <summary>
/// One parsed field of a cron expression: the set of values it allows as a bit mask, and whether the text started with
/// <c>*</c>. The wildcard flag matters twice: the day-of-month and day-of-week rule, and the repeated hour of a clock change.
/// </summary>
internal sealed class CronField
{
    private static readonly string[] MonthNames = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];
    private static readonly string[] DayNames = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    private readonly ulong _mask;

    private CronField(ulong mask, bool isWildcard)
    {
        _mask = mask;
        IsWildcard = isWildcard;
    }

    /// <summary>Gets whether the field text started with <c>*</c>, for example <c>*</c> or <c>*/15</c>.</summary>
    public bool IsWildcard { get; }

    /// <summary>Parses one field.</summary>
    /// <param name="text">The field text: a comma separated list of values, ranges and steps.</param>
    /// <param name="kind">Which field it is.</param>
    /// <param name="field">The parsed field when the call succeeds.</param>
    /// <param name="error">The reason when the call fails. It never repeats the text.</param>
    /// <returns>True when the field is valid.</returns>
    public static bool TryParse(string text, CronFieldKind kind, [NotNullWhen(true)] out CronField? field, out string error)
    {
        ArgumentNullException.ThrowIfNull(text);
        field = null;
        var name = FieldName(kind);
        ulong mask = 0;
        foreach (var item in text.Split(','))
        {
            if (!TryParseItem(item, kind, out var itemMask, out var itemError))
            {
                error = $"The {name} field is invalid: {itemError}";
                return false;
            }

            mask |= itemMask;
        }

        if (mask == 0)
        {
            error = $"The {name} field allows no value.";
            return false;
        }

        field = new CronField(mask, text.StartsWith('*'));
        error = string.Empty;
        return true;
    }

    /// <summary>Returns whether the field allows a value.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>True when the value is allowed.</returns>
    public bool Contains(int value) => value is >= 0 and < 64 && ((_mask >> value) & 1UL) != 0;

    /// <summary>Returns the smallest allowed value that is at least <paramref name="from"/>.</summary>
    /// <param name="from">The first value to consider, 0 to 63.</param>
    /// <returns>The value, or -1 when none is left.</returns>
    public int NextAtLeast(int from)
    {
        if (from is < 0 or > 63)
        {
            return -1;
        }

        var remaining = (_mask >> from) << from;
        return remaining == 0 ? -1 : BitOperations.TrailingZeroCount(remaining);
    }

    private static string FieldName(CronFieldKind kind) => kind switch
    {
        CronFieldKind.Minute => "minute",
        CronFieldKind.Hour => "hour",
        CronFieldKind.DayOfMonth => "day-of-month",
        CronFieldKind.Month => "month",
        _ => "day-of-week",
    };

    private static (int Min, int Max) Bounds(CronFieldKind kind) => kind switch
    {
        CronFieldKind.Minute => (0, 59),
        CronFieldKind.Hour => (0, 23),
        CronFieldKind.DayOfMonth => (1, 31),
        CronFieldKind.Month => (1, 12),
        _ => (0, 7),
    };

    private static bool TryParseItem(string item, CronFieldKind kind, out ulong mask, out string error)
    {
        mask = 0;
        if (item.Length == 0)
        {
            error = "an empty list entry.";
            return false;
        }

        var slash = item.IndexOf('/', StringComparison.Ordinal);
        var rangeText = slash < 0 ? item : item[..slash];
        var step = 1;
        if (slash >= 0 && !TryParseStep(item[(slash + 1)..], out step))
        {
            error = "a step must be a positive whole number.";
            return false;
        }

        if (!TryParseRange(rangeText, kind, slash >= 0, out var low, out var high, out error))
        {
            return false;
        }

        var (min, max) = Bounds(kind);
        if (low < min || high > max || low > high)
        {
            error = $"a value or range is outside {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        for (var value = low; value <= high; value += step)
        {
            var bit = kind == CronFieldKind.DayOfWeek && value == 7 ? 0 : value;
            mask |= 1UL << bit;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryParseStep(string text, out int step)
    {
        step = 0;
        return text.Length is > 0 and <= 4
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out step)
            && step >= 1;
    }

    private static bool TryParseRange(string text, CronFieldKind kind, bool hasStep, out int low, out int high, out string error)
    {
        var (min, max) = Bounds(kind);
        error = string.Empty;
        if (string.Equals(text, "*", StringComparison.Ordinal))
        {
            low = min;
            high = kind == CronFieldKind.DayOfWeek ? 6 : max;
            return true;
        }

        var dash = text.IndexOf('-', StringComparison.Ordinal);
        if (dash < 0)
        {
            high = 0;
            if (!TryParseValue(text, kind, out low))
            {
                error = "a value is not a number or a known name.";
                return false;
            }

            high = hasStep ? max : low;
            return true;
        }

        high = 0;
        if (!TryParseValue(text[..dash], kind, out low) || !TryParseValue(text[(dash + 1)..], kind, out high))
        {
            error = "a range bound is not a number or a known name.";
            return false;
        }

        return true;
    }

    private static bool TryParseValue(string text, CronFieldKind kind, out int value)
    {
        value = 0;
        if (text.Length == 0)
        {
            return false;
        }

        if (char.IsAsciiDigit(text[0]))
        {
            return text.Length <= 4 && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        var names = kind switch
        {
            CronFieldKind.Month => MonthNames,
            CronFieldKind.DayOfWeek => DayNames,
            _ => null,
        };
        if (names is null)
        {
            return false;
        }

        for (var index = 0; index < names.Length; index++)
        {
            if (string.Equals(names[index], text, StringComparison.OrdinalIgnoreCase))
            {
                value = kind == CronFieldKind.Month ? index + 1 : index;
                return true;
            }
        }

        return false;
    }
}
