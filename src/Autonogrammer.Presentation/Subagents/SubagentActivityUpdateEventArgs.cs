namespace Autonogrammer.Presentation.Subagents;

/// <summary>
/// One change to a task call. A null <see cref="AgentName"/> means the frame did not name the agent. A null
/// <see cref="Status"/> means the frame did not change status. A null <see cref="OutputLine"/> means the frame carried no output.
/// </summary>
public sealed class SubagentActivityUpdateEventArgs : EventArgs, IEquatable<SubagentActivityUpdateEventArgs>
{
    /// <summary>Creates an update for one task call.</summary>
    /// <param name="callId">The task call identifier. Never empty.</param>
    /// <param name="agentName">The agent name from the frame, or null when the frame did not name one.</param>
    /// <param name="status">The status from the frame, or null when the frame did not carry one.</param>
    /// <param name="outputLine">The latest output line as plain text, or null when the frame carried no output.</param>
    public SubagentActivityUpdateEventArgs(string callId, string? agentName, SubagentStatus? status, string? outputLine)
    {
        CallId = callId;
        AgentName = agentName;
        Status = status;
        OutputLine = outputLine;
    }

    /// <summary>Gets the task call identifier.</summary>
    public string CallId { get; }

    /// <summary>Gets the agent name from the frame, or null when the frame did not name one.</summary>
    public string? AgentName { get; }

    /// <summary>Gets the status from the frame, or null when the frame did not carry one.</summary>
    public SubagentStatus? Status { get; }

    /// <summary>Gets the latest output line as plain text, or null when the frame carried no output.</summary>
    public string? OutputLine { get; }

    /// <inheritdoc />
    public bool Equals(SubagentActivityUpdateEventArgs? other) =>
        other is not null
        && string.Equals(CallId, other.CallId, StringComparison.Ordinal)
        && string.Equals(AgentName, other.AgentName, StringComparison.Ordinal)
        && Status == other.Status
        && string.Equals(OutputLine, other.OutputLine, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as SubagentActivityUpdateEventArgs);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(CallId, AgentName, Status, OutputLine);
}
