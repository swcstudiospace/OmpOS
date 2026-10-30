namespace Autonogrammer.Presentation.Automations;

/// <summary>One run of a hosted automation, as an <see cref="IAutomationsManager"/> reports it.</summary>
/// <param name="RunId">The run identifier.</param>
/// <param name="Trigger">What started the run: <c>cron</c>, <c>webhook</c> or <c>manual</c>.</param>
/// <param name="Summary">A short account of what the run did, or an empty string when it gave none.</param>
/// <param name="Status">How the run stands or ended.</param>
/// <param name="StartedAt">When the run started.</param>
/// <param name="Duration">How long the run took, or null while it runs or when the duration is not known.</param>
/// <param name="Error">Why the run failed, or an empty string when it did not.</param>
/// <param name="ExternalId">The identifier of the cloud agent or session the run started, or an empty string when it started none.</param>
public sealed record HostedRunEntry(
    string RunId,
    string Trigger,
    string Summary,
    HostedRunStatus Status,
    DateTimeOffset StartedAt,
    TimeSpan? Duration,
    string Error,
    string ExternalId);
