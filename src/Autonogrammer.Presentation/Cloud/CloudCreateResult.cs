namespace Autonogrammer.Presentation.Cloud;

/// <summary>The result of an attempt to create a cloud agent: how it ended, what to tell the user, and the agent when one was created.</summary>
/// <param name="Outcome">How the attempt ended.</param>
/// <param name="Message">The plain sentence to show.</param>
/// <param name="Agent">The new agent for <see cref="CloudCreateOutcome.Created"/>; otherwise null.</param>
public sealed record CloudCreateResult(CloudCreateOutcome Outcome, string Message, CloudAgentRecord? Agent = null)
{
    /// <summary>Gets whether an agent was created.</summary>
    public bool IsCreated => Outcome == CloudCreateOutcome.Created;
}
