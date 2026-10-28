namespace Autonogrammer.Presentation.Automations;

/// <summary>How one run of a hosted automation stands or ended.</summary>
public enum HostedRunStatus
{
    /// <summary>The run is in progress.</summary>
    Running,

    /// <summary>The run finished and did what it was asked to do.</summary>
    Succeeded,

    /// <summary>The run started a cloud agent that carries on by itself; the run does not wait for its end.</summary>
    Launched,

    /// <summary>The run ended with an error.</summary>
    Failed,

    /// <summary>The run was stopped because it exceeded its maximum runtime.</summary>
    TimedOut,

    /// <summary>The run was stopped on request.</summary>
    Cancelled,

    /// <summary>The run did not start, for example because a limit on concurrent runs or runs per hour was reached.</summary>
    Skipped,
}
