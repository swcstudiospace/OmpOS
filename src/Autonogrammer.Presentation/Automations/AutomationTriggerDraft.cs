namespace Autonogrammer.Presentation.Automations;

/// <summary>One trigger of an automation: a cron schedule or a signed webhook.</summary>
/// <param name="Kind">The trigger kind: <c>cron</c> or <c>webhook</c>.</param>
/// <param name="Cron">The cron expression of a <c>cron</c> trigger, or an empty string.</param>
/// <param name="Flavour">The webhook flavour, <c>generic</c> or <c>github</c>, of a <c>webhook</c> trigger, or an empty string.</param>
/// <param name="Events">The GitHub event names a <c>github</c> webhook accepts; empty for every other trigger.</param>
/// <param name="Actions">The GitHub action names a <c>github</c> webhook accepts; empty for every other trigger.</param>
/// <param name="Branches">The branch names or globs a <c>github</c> webhook accepts; empty for every other trigger.</param>
public sealed record AutomationTriggerDraft(
    string Kind,
    string Cron,
    string Flavour,
    IReadOnlyList<string> Events,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Branches);
