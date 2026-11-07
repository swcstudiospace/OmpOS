using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Presentation.Export;

namespace Autonogrammer.Presentation.Usage;

/// <summary>
/// Per-session and per-day usage summed from end-of-turn reports. Occupancy is never added across turns. A session's cost is
/// its last reported amount. A day's cost is the sum of those session amounts. There are no plan limits and no billing.
/// </summary>
public sealed class UsageMeterModel
{
    /// <summary>The words shown when the engine reported no cost. Never a zero.</summary>
    public const string UsageUnavailable = "UsageUnavailable";

    /// <summary>The model name written on an export record. The ledger has no model, and this is not a plan or a bill.</summary>
    public const string ExportModelName = "local";

    private UsageMeterModel(IReadOnlyList<SessionRow> sessions, IReadOnlyList<UsageDayModel> days, bool costUnavailable)
    {
        Sessions = sessions;
        Days = days;
        CostUnavailable = costUnavailable;
    }

    /// <summary>Gets an empty meter. Cost is not unavailable; there is simply nothing to show.</summary>
    public static UsageMeterModel Empty { get; } = new([], [], costUnavailable: false);

    /// <summary>Gets one row per session, ordered by session identifier.</summary>
    public IReadOnlyList<SessionRow> Sessions { get; }

    /// <summary>Gets one row per day, in chronological order.</summary>
    public IReadOnlyList<UsageDayModel> Days { get; }

    /// <summary>Gets whether every report lacked a cost, so the meter shows <see cref="UsageUnavailable"/> instead of zero.</summary>
    public bool CostUnavailable { get; }

    /// <summary>Gets <see cref="UsageUnavailable"/> when <see cref="CostUnavailable"/> is true, otherwise empty.</summary>
    public string UnavailableText => CostUnavailable ? UsageUnavailable : string.Empty;

    /// <summary>
    /// Sums the reports. <paramref name="samples"/> is append order. Occupancy is the last and the peak of a session.
    /// Cost is the last amount of a session (OQ-460: treated as session-cumulative). A day's cost sums each session's last
    /// amount that day, and is unavailable when any of those sessions reported no cost.
    /// </summary>
    /// <param name="samples">The end-of-turn reports. Not modified.</param>
    /// <returns>The meter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="samples"/> or one sample is null.</exception>
    public static UsageMeterModel Create(IEnumerable<Sample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var rows = new List<Sample>();
        foreach (var sample in samples)
        {
            ArgumentNullException.ThrowIfNull(sample, nameof(samples));
            rows.Add(sample);
        }

        if (rows.Count == 0)
        {
            return Empty;
        }

        var sessions = rows
            .GroupBy(static sample => sample.SessionId, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => SessionRow.From(group.Key, group.ToList()))
            .ToList()
            .AsReadOnly();

        var days = rows
            .GroupBy(static sample => sample.Day)
            .OrderBy(static group => group.Key)
            .Select(static group => DayFrom(group.Key, group.ToList()))
            .ToList()
            .AsReadOnly();

        return new UsageMeterModel(sessions, days, rows.All(static sample => sample.Amount is null));
    }

    /// <summary>
    /// Builds export history from the same reports: one run per session and day, carrying that day's last occupancy as input
    /// tokens. Output tokens stay zero because occupancy is not a token delta.
    /// </summary>
    /// <param name="samples">The end-of-turn reports.</param>
    /// <returns>The history. Empty reports produce an empty history.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="samples"/> is null.</exception>
    public static UsageHistory ToHistory(IEnumerable<Sample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var records = new List<UsageRecord>();
        foreach (var sample in Collapse(samples))
        {
            records.Add(new UsageRecord(
                sample.SessionId + "/" + sample.Day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                new DateTimeOffset(sample.Day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                ExportModelName,
                sample.Used,
                outputTokens: 0));
        }

        return new UsageHistory(records);
    }

    /// <summary>Returns the type name. Amounts and occupancy stay out of logs.</summary>
    public override string ToString() => nameof(UsageMeterModel);

    private static IEnumerable<Sample> Collapse(IEnumerable<Sample> samples)
    {
        return samples
            .GroupBy(static sample => (sample.SessionId, sample.Day))
            .Select(static group => group.Last());
    }

    private static UsageDayModel DayFrom(DateOnly day, IReadOnlyList<Sample> rows)
    {
        var bySession = rows.GroupBy(static sample => sample.SessionId, StringComparer.Ordinal).ToList();
        var missing = bySession.Any(static group => group.Last().Amount is null);
        decimal? amount = missing ? null : bySession.Sum(static group => group.Last().Amount!.Value);
        string? currency = missing ? null : bySession.Select(static group => group.Last().Currency).LastOrDefault(static value => !string.IsNullOrEmpty(value));
        return new UsageDayModel(day, bySession.Count, rows.Max(static sample => sample.Used), rows[^1].Used, amount, currency, missing);
    }

    /// <summary>One end-of-turn report the meter sums. It has no prompt content.</summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "The sample is the Presentation source row of this model; a second file would leave the owned file list.")]
    public sealed class Sample
    {
        /// <summary>Creates a report.</summary>
        /// <param name="sessionId">The session. Not empty.</param>
        /// <param name="day">The UTC day of the report.</param>
        /// <param name="size">The context window. Not negative.</param>
        /// <param name="used">The occupancy. Not negative.</param>
        /// <param name="amount">The session-cumulative amount, or null when the engine reported none.</param>
        /// <param name="currency">The currency, required when <paramref name="amount"/> is set.</param>
        /// <exception cref="ArgumentException"><paramref name="sessionId"/> is empty, or a cost has no currency.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A token count is negative.</exception>
        public Sample(string sessionId, DateOnly day, long size, long used, decimal? amount, string? currency)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            ArgumentOutOfRangeException.ThrowIfNegative(size);
            ArgumentOutOfRangeException.ThrowIfNegative(used);
            if (amount is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(currency);
            }

            SessionId = sessionId;
            Day = day;
            Size = size;
            Used = used;
            Amount = amount;
            Currency = currency;
        }

        /// <summary>Gets the session.</summary>
        public string SessionId { get; }

        /// <summary>Gets the UTC day.</summary>
        public DateOnly Day { get; }

        /// <summary>Gets the context window.</summary>
        public long Size { get; }

        /// <summary>Gets the occupancy. It is not a delta.</summary>
        public long Used { get; }

        /// <summary>Gets the session-cumulative amount, or null.</summary>
        public decimal? Amount { get; }

        /// <summary>Gets the currency, or null when there is no amount.</summary>
        public string? Currency { get; }

        /// <summary>Returns the type name.</summary>
        public override string ToString() => nameof(Sample);
    }

    /// <summary>One session: last and peak occupancy, and the last reported cost.</summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "The session row is part of the meter; a second file would leave the owned file list.")]
    public sealed class SessionRow
    {
        private SessionRow(string sessionId, long size, long lastUsed, long peakUsed, decimal? amount, string? currency, bool unavailable)
        {
            SessionId = sessionId;
            Size = size;
            LastUsed = lastUsed;
            PeakUsed = peakUsed;
            Amount = amount;
            Currency = currency;
            Unavailable = unavailable;
        }

        /// <summary>Gets the session identifier.</summary>
        public string SessionId { get; }

        /// <summary>Gets the context window of the last report.</summary>
        public long Size { get; }

        /// <summary>Gets the occupancy of the last report.</summary>
        public long LastUsed { get; }

        /// <summary>Gets the highest occupancy of the session.</summary>
        public long PeakUsed { get; }

        /// <summary>Gets the last reported amount, or null when that report had none.</summary>
        public decimal? Amount { get; }

        /// <summary>Gets the currency of <see cref="Amount"/>.</summary>
        public string? Currency { get; }

        /// <summary>Gets whether this session shows <see cref="UsageMeterModel.UsageUnavailable"/> instead of zero.</summary>
        public bool Unavailable { get; }

        /// <summary>Returns the type name.</summary>
        public override string ToString() => nameof(SessionRow);

        internal static SessionRow From(string sessionId, IReadOnlyList<Sample> rows)
        {
            var last = rows[^1];
            return new SessionRow(
                sessionId,
                last.Size,
                last.Used,
                rows.Max(static sample => sample.Used),
                last.Amount,
                last.Currency,
                last.Amount is null);
        }
    }
}
