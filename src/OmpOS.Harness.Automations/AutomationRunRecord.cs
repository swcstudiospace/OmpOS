namespace OmpOS.Harness.Automations;

/// <summary>
/// One decision about one run attempt. It records what was attempted, at which chain depth, from which origin, how it
/// ended, and the guard and reason behind any block, refusal or timeout.
/// </summary>
/// <param name="AutomationId">The automation the attempt was for.</param>
/// <param name="Depth">The chain depth of the attempt. A manual, scheduled or external run is depth 1.</param>
/// <param name="Origin">"manual", "schedule", or the origin of the event that triggered the attempt.</param>
/// <param name="Outcome">How the attempt ended.</param>
/// <param name="Guard">The guard that blocked, refused or stopped the attempt, or null when none did.</param>
/// <param name="Reason">A human-readable reason.</param>
/// <param name="Pause">The pause that refused the attempt, when the automation was paused.</param>
/// <param name="RecordedAt">The injected clock's time when the decision was recorded.</param>
public sealed record AutomationRunRecord(
    string AutomationId,
    int Depth,
    string Origin,
    AutomationRunOutcome Outcome,
    string? Guard,
    string Reason,
    AutomationPause? Pause,
    DateTimeOffset RecordedAt);
