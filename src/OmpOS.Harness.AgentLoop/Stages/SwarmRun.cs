namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>The observed state of one subagent run.</summary>
internal sealed class SwarmRun
{
    /// <summary>Creates a run record.</summary>
    /// <param name="id">The validated run or subagent identifier.</param>
    public SwarmRun(string id)
    {
        Id = id;
        Label = id;
    }

    /// <summary>Gets the run or subagent identifier. It contains only letters, digits, dot, underscore and hyphen.</summary>
    public string Id { get; }

    /// <summary>Gets or sets the short display title: the agent name when known, otherwise the identifier.</summary>
    public string Label { get; set; }

    /// <summary>Gets or sets the phase of the run.</summary>
    public SwarmRunPhase Phase { get; set; } = SwarmRunPhase.Waiting;

    /// <summary>Gets or sets the cleaned raw state word, kept for <see cref="SwarmRunPhase.Unknown"/>.</summary>
    public string? RawState { get; set; }

    /// <summary>Gets or sets a value saying the run is working but silent.</summary>
    public bool Stalled { get; set; }

    /// <summary>Gets or sets the error code of a failed run, or null. Only a code shaped like <c>E-TIMEOUT</c> is kept.</summary>
    public string? ErrorCode { get; set; }
}
