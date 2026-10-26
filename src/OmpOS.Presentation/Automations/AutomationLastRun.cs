namespace OmpOS.Presentation.Automations;

/// <summary>The latest run attempt of an automation, as an <see cref="IAutomationsSource"/> reports it.</summary>
/// <param name="Outcome">How the attempt ended.</param>
/// <param name="Reason">A human-readable reason, or an empty string when the attempt gave none.</param>
/// <param name="RecordedAt">When the attempt was recorded.</param>
public sealed record AutomationLastRun(AutomationOutcome Outcome, string Reason, DateTimeOffset RecordedAt);
