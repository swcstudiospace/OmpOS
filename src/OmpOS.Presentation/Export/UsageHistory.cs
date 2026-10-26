using System.Globalization;

namespace OmpOS.Presentation.Export;

/// <summary>
/// The usage of a set of runs: the runs in order, and their totals per day or per month.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Runs"/> is ordered by timestamp, then by run identifier with ordinal comparison. Two records with the same
/// timestamp and identifier keep the order they were supplied in, so the listing never changes between calls.
/// </para>
/// <para>
/// Totals are ordered by period key, which is chronological because the keys are zero-padded and in UTC.
/// </para>
/// </remarks>
public sealed class UsageHistory
{
    private readonly IReadOnlyList<UsageRecord> _runs;

    /// <summary>Creates a usage history over the given runs.</summary>
    /// <param name="runs">The usage records. Not modified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runs"/> or one of its records is null.</exception>
    public UsageHistory(IEnumerable<UsageRecord> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);

        var ordered = new List<UsageRecord>();
        foreach (var record in runs)
        {
            ArgumentNullException.ThrowIfNull(record, nameof(runs));
            ordered.Add(record);
        }

        _runs = ordered
            .OrderBy(static record => record.TimestampUtc)
            .ThenBy(static record => record.RunId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>Gets the runs, ordered by timestamp and then by run identifier.</summary>
    public IReadOnlyList<UsageRecord> Runs => _runs;

    /// <summary>Totals the runs per period, in chronological order. An empty history gives no totals.</summary>
    /// <param name="period">The period to group by.</param>
    /// <returns>One total per period that has at least one run.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period"/> is not a defined value.</exception>
    public IReadOnlyList<UsageTotal> Totals(UsagePeriod period)
    {
        var format = period switch
        {
            UsagePeriod.Day => "yyyy-MM-dd",
            UsagePeriod.Month => "yyyy-MM",
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown usage period."),
        };

        return _runs
            .GroupBy(record => DateOnly.FromDateTime(record.TimestampUtc.UtcDateTime).ToString(format, CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new UsageTotal(
                group.Key,
                group.Count(),
                group.Sum(static record => record.InputTokens),
                group.Sum(static record => record.OutputTokens)))
            .ToList()
            .AsReadOnly();
    }
}
