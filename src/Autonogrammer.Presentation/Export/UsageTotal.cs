namespace Autonogrammer.Presentation.Export;

/// <summary>The summed usage of every run that falls in one period.</summary>
/// <param name="Period">The period key: <c>yyyy-MM-dd</c> for a day, <c>yyyy-MM</c> for a month, in UTC.</param>
/// <param name="RunCount">The number of runs in the period.</param>
/// <param name="InputTokens">The input tokens summed over the period.</param>
/// <param name="OutputTokens">The output tokens summed over the period.</param>
public sealed record UsageTotal(string Period, int RunCount, long InputTokens, long OutputTokens)
{
    /// <summary>Gets the input and output tokens together.</summary>
    public long TotalTokens => InputTokens + OutputTokens;
}
