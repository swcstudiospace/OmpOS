namespace OmpOS.Presentation.Cloud;

/// <summary>The result of a hand-off: how it ended, what to tell the user and the agent when one was created.</summary>
/// <param name="Outcome">How the hand-off ended.</param>
/// <param name="Message">The plain sentence to show.</param>
/// <param name="Agent">The new agent for <see cref="CloudHandoffOutcome.Started"/>; otherwise null.</param>
public sealed record CloudHandoffResult(CloudHandoffOutcome Outcome, string Message, CloudAgentRecord? Agent = null);
