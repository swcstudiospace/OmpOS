using OmpOS.Harness.Automations.Cron;
using OmpOS.Harness.Automations.Hosted;
using OmpOS.Presentation.Automations;
using ViewDraft = OmpOS.Presentation.Automations.AutomationDraft;

namespace OmpOS.Native.Composition.Automations;

/// <summary>
/// The automations page's manager on the desktop: the automations live in a file of this computer, the schedule is the only trigger (a webhook needs the OmpOS service, which has a public address), and a
/// run goes to a local agent session or, when the head has a cloud agents service, to a cloud agent. It adapts the automation harness (<see cref="AutomationService"/>, <see cref="AutomationRunner"/>) to
/// <see cref="IAutomationsManager"/>.
/// </summary>
internal sealed class LocalAutomationsManager : IAutomationsManager
{
    /// <summary>The owner every automation of this computer has: the file holds the automations of the one user of this head.</summary>
    internal const string Owner = "local";

    private readonly AutomationService _service;
    private readonly IAutomationStore _store;
    private readonly AutomationRunner _runner;
    private readonly TimeProvider _clock;
    private readonly bool _localAvailable;
    private readonly bool _cloudAvailable;

    /// <summary>Initializes a new instance of the <see cref="LocalAutomationsManager"/> class.</summary>
    /// <param name="service">The service that validates and saves automations.</param>
    /// <param name="store">The store of the automations and their run history.</param>
    /// <param name="runner">The runner a run now goes through.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localAvailable">Whether this computer can run a local automation (omp was found).</param>
    /// <param name="cloudAvailable">Whether the head has a cloud agents service.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public LocalAutomationsManager(AutomationService service, IAutomationStore store, AutomationRunner runner, TimeProvider clock, bool localAvailable, bool cloudAvailable)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(clock);
        _service = service;
        _store = store;
        _runner = runner;
        _clock = clock;
        _localAvailable = localAvailable;
        _cloudAvailable = cloudAvailable;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedTargetKinds =>
        [.. new[] { _localAvailable ? AutomationDraftMapper.LocalKind : null, _cloudAvailable ? AutomationDraftMapper.CloudKind : null }.OfType<string>()];

    /// <inheritdoc/>
    public bool SupportsWebhooks => false;

    /// <inheritdoc/>
    public string PageNote => _localAvailable ? AutomationsCopy.DesktopNote : AutomationsCopy.NoOmpNote;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HostedAutomationEntry>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<UserAutomation> automations;
        try
        {
            automations = await _service.ListAsync(Owner, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            throw new AutomationsSourceException("The automations file could not be read.", exception);
        }

        var entries = new List<HostedAutomationEntry>(automations.Count);
        foreach (var automation in automations)
        {
            entries.Add(await ToEntryAsync(automation, cancellationToken).ConfigureAwait(false));
        }

        return entries;
    }

    /// <inheritdoc/>
    public async Task<AutomationSaveResult> SaveAsync(string? id, ViewDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Triggers.Any(static trigger => string.Equals(trigger.Kind, AutomationDraftMapper.WebhookKind, StringComparison.Ordinal)))
        {
            throw new AutomationsSourceException(AutomationsCopy.WebhookNotSupported);
        }

        if (!SupportedTargetKinds.Contains(draft.TargetKind, StringComparer.Ordinal))
        {
            throw new AutomationsSourceException(string.Equals(draft.TargetKind, AutomationDraftMapper.LocalKind, StringComparison.Ordinal) ? AutomationsCopy.NoOmpNote : AutomationsCopy.CloudNotConfigured);
        }

        var hosted = AutomationDraftMapper.ToHosted(draft);
        AutomationChange change;
        try
        {
            change = id is null
                ? await _service.CreateAsync(Owner, hosted, cancellationToken).ConfigureAwait(false)
                : await _service.ReplaceAsync(Owner, id, hosted, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AutomationsSourceException("The automation could not be saved.", exception);
        }

        var saved = Require(change);
        return new AutomationSaveResult(await ToEntryAsync(saved, cancellationToken).ConfigureAwait(false), webhookSecret: null);
    }

    /// <inheritdoc/>
    public async Task SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _ = Require(await _service.SetEnabledAsync(Owner, id, enabled, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _ = await _service.DeleteAsync(Owner, id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<HostedRunEntry> RunNowAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var automation = await _service.GetAsync(Owner, id, cancellationToken).ConfigureAwait(false)
            ?? throw new AutomationsSourceException("The automation no longer exists.");
        var start = await _runner.StartAsync(automation, AutomationTriggerContext.Manual(), cancellationToken).ConfigureAwait(false);
        if (!start.Admitted)
        {
            throw new AutomationsSourceException(start.Refusal switch
            {
                AutomationRunRefusal.ConcurrencyLimit => "The automation is already running. It starts again when the run ends.",
                AutomationRunRefusal.HourlyCap => "The automation reached its limit of runs per hour.",
                _ => "OmpOS is closing, so no run starts.",
            });
        }

        var history = await _store.GetHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        var entry = history.FirstOrDefault(run => string.Equals(run.RunId, start.RunId, StringComparison.Ordinal))
            ?? throw new AutomationsSourceException("The run was started but its record could not be read.");
        return AutomationDraftMapper.ToView(entry);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HostedRunEntry>> HistoryAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var history = await _store.GetHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        return [.. history.OrderByDescending(static run => run.StartedAt).Select(AutomationDraftMapper.ToView)];
    }

    /// <inheritdoc/>
    public Task<AutomationCronPreview> PreviewCronAsync(string expression, string timeZone, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(timeZone);
        if (!CronExpression.TryParse(expression, out var cron, out var error))
        {
            return Task.FromResult(new AutomationCronPreview([], error));
        }

        if (!AutomationTimeZones.TryResolve(timeZone, out var zone))
        {
            return Task.FromResult(new AutomationCronPreview([], "The time zone is not known on this computer."));
        }

        var runs = cron.GetNextOccurrences(_clock.GetUtcNow(), zone, Math.Clamp(count, 1, CronExpression.MaxOccurrences));
        return Task.FromResult(new AutomationCronPreview(runs, runs.Count == 0 ? AutomationsCopy.CronNeverRuns : string.Empty));
    }

    /// <inheritdoc/>
    public Task<string> RotateWebhookSecretAsync(string id, CancellationToken cancellationToken) =>
        throw new AutomationsSourceException(AutomationsCopy.WebhookNotSupported);

    private static UserAutomation Require(AutomationChange change)
    {
        if (change.Automation is { } automation)
        {
            return automation;
        }

        throw new AutomationsSourceException(
            change.NotFound
                ? "The automation no longer exists."
                : string.Join(' ', change.Issues.Select(static issue => issue.Message)));
    }

    private async Task<HostedAutomationEntry> ToEntryAsync(UserAutomation automation, CancellationToken cancellationToken)
    {
        var history = await _store.GetHistoryAsync(automation.Id, cancellationToken).ConfigureAwait(false);
        var last = history.OrderByDescending(static run => run.StartedAt).FirstOrDefault();
        DateTimeOffset? next = automation.Enabled ? AutomationNextRun.Next(automation, _clock.GetUtcNow()) : null;
        return new HostedAutomationEntry(
            automation.Id,
            AutomationDraftMapper.ToView(automation),
            next,
            last is null ? null : AutomationDraftMapper.ToView(last),
            WebhookPath: string.Empty,
            HasWebhookSecret: false);
    }
}
