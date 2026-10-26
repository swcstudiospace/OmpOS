using System.Globalization;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// Formats how long ago something happened for the Agent Panel: <c>just now</c> under 10 s, <c>{n} s ago</c> under 60 s,
/// <c>{n} min ago</c> under 60 min, <c>{n} h ago</c> under 24 h and <c>{n} d ago</c> beyond. A time in the future reads <c>just now</c>.
/// </summary>
public static class RelativeTimeText
{
    private static readonly TimeSpan JustNowLimit = TimeSpan.FromSeconds(10);

    /// <summary>Formats the time between <paramref name="happenedAt"/> and <paramref name="now"/>.</summary>
    /// <param name="happenedAt">The time the thing happened.</param>
    /// <param name="now">The current time, from an injected <see cref="TimeProvider"/>.</param>
    /// <returns>The relative text.</returns>
    public static string Format(DateTimeOffset happenedAt, DateTimeOffset now) => Format(now - happenedAt);

    /// <summary>Formats an age.</summary>
    /// <param name="age">How long ago the thing happened. A negative age is treated as zero.</param>
    /// <returns>The relative text.</returns>
    public static string Format(TimeSpan age)
    {
        if (age < JustNowLimit)
        {
            return "just now";
        }

        if (age < TimeSpan.FromMinutes(1))
        {
            return Ago((long)age.TotalSeconds, "s");
        }

        if (age < TimeSpan.FromHours(1))
        {
            return Ago((long)age.TotalMinutes, "min");
        }

        return age < TimeSpan.FromDays(1) ? Ago((long)age.TotalHours, "h") : Ago((long)age.TotalDays, "d");
    }

    private static string Ago(long count, string unit) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {unit} ago");
}
