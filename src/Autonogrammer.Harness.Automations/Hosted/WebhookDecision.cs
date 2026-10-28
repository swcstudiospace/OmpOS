namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// The result of <see cref="WebhookTrigger.Evaluate"/>. The reason is a stable code for the server's own log: it is never sent to the
/// caller of an unauthorized delivery, so the receiver does not tell an attacker which check failed.
/// </summary>
/// <param name="Outcome">What was decided.</param>
/// <param name="Reason">A stable lower-case code such as <c>bad_signature</c> or <c>branch_filter</c>. It holds no secret and no payload text.</param>
/// <param name="Context">The context of the run to start, for <see cref="WebhookOutcome.Accepted"/>; otherwise null.</param>
public sealed record WebhookDecision(WebhookOutcome Outcome, string Reason, AutomationTriggerContext? Context = null);
