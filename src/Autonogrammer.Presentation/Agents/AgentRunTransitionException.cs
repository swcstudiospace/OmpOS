namespace Autonogrammer.Presentation.Agents;

/// <summary>
/// Raised when <see cref="AgentRunRegistry.Transition"/> asks for a status change that the transition table does not allow.
/// The run keeps its status.
/// </summary>
public sealed class AgentRunTransitionException : InvalidOperationException
{
    /// <summary>Creates an exception with no transition details.</summary>
    public AgentRunTransitionException()
    {
    }

    /// <summary>Creates an exception with a message and no transition details.</summary>
    /// <param name="message">The message.</param>
    public AgentRunTransitionException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and an inner exception, and no transition details.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public AgentRunTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a refused transition.</summary>
    /// <param name="runId">The run whose transition was refused.</param>
    /// <param name="from">The status the run had.</param>
    /// <param name="to">The status that was requested.</param>
    public AgentRunTransitionException(string runId, AgentRunStatus from, AgentRunStatus to)
        : base($"Run {runId} cannot change from {from} to {to}.")
    {
        RunId = runId;
        From = from;
        To = to;
    }

    /// <summary>Gets the run whose transition was refused, or null when the exception carries no transition details.</summary>
    public string? RunId { get; }

    /// <summary>Gets the status the run had when the transition was refused, or null when the exception carries no transition details.</summary>
    public AgentRunStatus? From { get; }

    /// <summary>Gets the status that was requested, or null when the exception carries no transition details.</summary>
    public AgentRunStatus? To { get; }
}
