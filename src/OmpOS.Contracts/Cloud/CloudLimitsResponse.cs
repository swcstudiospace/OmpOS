namespace OmpOS.Contracts.Cloud;

/// <summary>The body of <c>GET /cloud/limits</c>: the caller's caps and what they use of them.</summary>
public sealed record CloudLimitsResponse
{
    /// <summary>Gets how many agents the caller may own.</summary>
    public required int MaxAgents { get; init; }

    /// <summary>Gets how many agents may be provisioning, starting or running at once.</summary>
    public required int MaxConcurrentRunning { get; init; }

    /// <summary>Gets how many agents the caller owns now.</summary>
    public required int Agents { get; init; }

    /// <summary>Gets how many of them are provisioning, starting or running now.</summary>
    public required int Running { get; init; }

    /// <summary>Gets the idle window applied when a request names none, in minutes.</summary>
    public required int DefaultIdleMinutes { get; init; }

    /// <summary>Gets the compute seconds one agent may use, or 0 for no limit.</summary>
    public required long MaxComputeSecondsPerAgent { get; init; }

    /// <summary>Gets the compute seconds all the caller's agents used so far.</summary>
    public required double ComputeSeconds { get; init; }
}
