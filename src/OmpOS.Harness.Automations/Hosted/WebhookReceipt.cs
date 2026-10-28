namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// What the webhook receiver did with one delivery.
/// </summary>
/// <param name="Outcome">The trigger's decision, or <see cref="WebhookOutcome.Accepted"/> when a run was admitted.</param>
/// <param name="Reason">A stable code for the server log. It is never sent to the caller of an unauthorized delivery.</param>
/// <param name="RunId">The admitted run's identifier, or null when no run started.</param>
/// <param name="Refusal">The guard that refused an authentic delivery's run, or null.</param>
public sealed record WebhookReceipt(WebhookOutcome Outcome, string Reason, string? RunId, AutomationRunRefusal? Refusal);
