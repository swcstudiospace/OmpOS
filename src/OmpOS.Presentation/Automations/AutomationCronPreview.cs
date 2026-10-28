namespace OmpOS.Presentation.Automations;

/// <summary>The next runs a cron expression produces, or why it produces none.</summary>
/// <param name="NextRuns">The next run times in order; empty when the expression is invalid or never fires.</param>
/// <param name="Error">Why the expression is invalid, or an empty string when it is valid.</param>
public sealed record AutomationCronPreview(IReadOnlyList<DateTimeOffset> NextRuns, string Error);
