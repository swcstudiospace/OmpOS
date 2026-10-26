namespace OmpOS.Contracts.Agents;

/// <summary>
/// The state of one stage of the agent loop. <see cref="Degraded"/> and <see cref="Unavailable"/> exist so that a failing or
/// missing plugin shows as a visible, explained state instead of an exception.
/// </summary>
public enum StageStatus
{
    /// <summary>The stage is known and has not started.</summary>
    Pending,

    /// <summary>The stage is running.</summary>
    Running,

    /// <summary>The stage finished.</summary>
    Succeeded,

    /// <summary>The stage ran with reduced function, or its output could not be read. The detail says what is missing.</summary>
    Degraded,

    /// <summary>The stage failed. The detail says why.</summary>
    Failed,

    /// <summary>The stage was skipped on purpose, for example because planning is switched off for the run.</summary>
    Skipped,

    /// <summary>The stage cannot run here, for example because a plugin is not installed or a service is not reachable.</summary>
    Unavailable,
}
