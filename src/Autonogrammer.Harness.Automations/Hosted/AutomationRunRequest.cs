namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// What an <see cref="IAutomationTarget"/> receives to start one run.
/// </summary>
/// <param name="Automation">The automation that runs.</param>
/// <param name="RunId">The run identifier. A target uses it as an idempotency key, so a retried start never creates a second agent.</param>
/// <param name="Prompt">The complete prompt, built by <see cref="AutomationPromptBuilder"/>.</param>
/// <param name="Trigger">What started the run.</param>
public sealed record AutomationRunRequest(UserAutomation Automation, string RunId, string Prompt, AutomationTriggerContext Trigger);
