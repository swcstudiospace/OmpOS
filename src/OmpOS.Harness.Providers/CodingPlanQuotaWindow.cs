namespace OmpOS.Harness.Providers;

/// <summary>The period over which a coding plan counts requests against one quota limit.</summary>
public enum CodingPlanQuotaWindow
{
    /// <summary>A sliding window of five hours.</summary>
    FiveHours = 0,

    /// <summary>A sliding window of seven days.</summary>
    Week,

    /// <summary>The subscription month, which ends when the plan's validity ends.</summary>
    SubscriptionMonth,
}
