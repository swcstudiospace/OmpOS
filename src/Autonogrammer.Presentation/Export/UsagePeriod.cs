namespace Autonogrammer.Presentation.Export;

/// <summary>The calendar period usage totals are grouped by. Periods are taken in UTC.</summary>
public enum UsagePeriod
{
    /// <summary>One calendar day, keyed as <c>yyyy-MM-dd</c>.</summary>
    Day,

    /// <summary>One calendar month, keyed as <c>yyyy-MM</c>.</summary>
    Month,
}
