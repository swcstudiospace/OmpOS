namespace Autonogrammer.Harness.Providers;

/// <summary>How a provider is reached and billed. Names follow the research groups in docs/plans/vision-research.</summary>
public enum ProviderKind
{
    /// <summary>A runtime on the user's own machine, reached on a loopback address.</summary>
    Local = 0,

    /// <summary>A user-supplied endpoint that speaks a documented compatible protocol.</summary>
    Custom,

    /// <summary>A hosted platform reached through a cloud account.</summary>
    Cloud,

    /// <summary>A hosted API reached with a provider API key and billed per use.</summary>
    ApiKey,

    /// <summary>A subscription plan whose separate coding key is used from a listed coding tool.</summary>
    CodingPlan,
}
