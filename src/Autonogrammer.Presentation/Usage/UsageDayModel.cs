namespace Autonogrammer.Presentation.Usage;

/// <summary>
/// One day's usage. Occupancy is the last and the peak reported that day, never a sum across turns. Cost is the sum of each
/// session's last reported amount that day, or unavailable when a session that day reported none.
/// </summary>
public sealed class UsageDayModel
{
    /// <summary>Creates a day row.</summary>
    /// <param name="day">The UTC day.</param>
    /// <param name="sessionCount">How many sessions reported that day.</param>
    /// <param name="peakUsed">The highest occupancy reported that day.</param>
    /// <param name="lastUsed">The occupancy of the last report that day.</param>
    /// <param name="amount">The sum of per-session last amounts, or null when cost is unavailable.</param>
    /// <param name="currency">The currency of <paramref name="amount"/>, or null when cost is unavailable.</param>
    /// <param name="unavailable">True when the day must show <see cref="UsageMeterModel.UsageUnavailable"/> instead of zero.</param>
    public UsageDayModel(DateOnly day, int sessionCount, long peakUsed, long lastUsed, decimal? amount, string? currency, bool unavailable)
    {
        Day = day;
        SessionCount = sessionCount;
        PeakUsed = peakUsed;
        LastUsed = lastUsed;
        Amount = amount;
        Currency = currency;
        Unavailable = unavailable;
    }

    /// <summary>Gets the UTC day.</summary>
    public DateOnly Day { get; }

    /// <summary>Gets how many sessions reported that day.</summary>
    public int SessionCount { get; }

    /// <summary>Gets the highest occupancy reported that day.</summary>
    public long PeakUsed { get; }

    /// <summary>Gets the occupancy of the last report that day.</summary>
    public long LastUsed { get; }

    /// <summary>Gets the summed session amounts, or null when cost is unavailable.</summary>
    public decimal? Amount { get; }

    /// <summary>Gets the currency of <see cref="Amount"/>, or null when cost is unavailable.</summary>
    public string? Currency { get; }

    /// <summary>Gets whether the day shows the unavailable state instead of a number.</summary>
    public bool Unavailable { get; }

    /// <summary>Returns the type name. Amounts and occupancy stay out of logs.</summary>
    public override string ToString() => nameof(UsageDayModel);
}
