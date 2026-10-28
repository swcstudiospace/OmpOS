namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// One trigger of an automation. An automation runs when any of its triggers fires. The fields that do not belong to the kind are
/// ignored: a cron trigger uses <see cref="Cron"/>; a webhook trigger uses <see cref="Flavour"/> and the filters.
/// </summary>
public sealed record AutomationTriggerSpec
{
    /// <summary>Gets the kind of trigger.</summary>
    public AutomationTriggerKind Kind { get; init; } = AutomationTriggerKind.Cron;

    /// <summary>Gets the cron expression of a cron trigger.</summary>
    public string? Cron { get; init; }

    /// <summary>Gets the sender a webhook trigger accepts.</summary>
    public WebhookFlavour Flavour { get; init; } = WebhookFlavour.Generic;

    /// <summary>Gets the GitHub events a webhook trigger accepts (<c>push</c>, <c>pull_request</c>). Empty accepts both. Not used by the generic flavour.</summary>
    public IReadOnlyList<string> Events { get; init; } = [];

    /// <summary>
    /// Gets the actions of a <c>pull_request</c> event the trigger accepts, for example <c>opened</c>, <c>synchronize</c>, <c>reopened</c>,
    /// <c>ready_for_review</c>, or the pseudo action <c>merged</c> (a pull request closed by a merge). Empty accepts every action.
    /// </summary>
    public IReadOnlyList<string> Actions { get; init; } = [];

    /// <summary>
    /// Gets the branch globs a GitHub trigger accepts: the pushed branch, or the base branch of a pull request. <c>*</c> matches within
    /// one path segment, <c>**</c> across segments, <c>?</c> one character. Empty accepts every branch.
    /// </summary>
    public IReadOnlyList<string> Branches { get; init; } = [];
}
