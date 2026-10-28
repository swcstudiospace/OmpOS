using OmpOS.Presentation.Automations;

namespace OmpOS.Presentation.Tests.Automations;

/// <summary>
/// An automations manager the test scripts: it keeps the automations in memory, applies saves, toggles and deletes to them, records every
/// call with its arguments, and can fail an operation with a scripted exception or hold it open until the test releases it.
/// </summary>
internal sealed class FakeAutomationsManager : IAutomationsManager
{
    private readonly List<HostedAutomationEntry> _entries = [];
    private readonly Dictionary<string, List<HostedRunEntry>> _runs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _failures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource> _gates = new(StringComparer.Ordinal);
    private int _counter;

    /// <summary>Creates a manager that holds automations.</summary>
    /// <param name="entries">The automations it starts with.</param>
    internal FakeAutomationsManager(params HostedAutomationEntry[] entries)
    {
        _entries.AddRange(entries);
    }

    /// <summary>Gets or sets the target kinds the manager reports as supported.</summary>
    public IReadOnlyList<string> SupportedTargetKinds { get; set; } = ["local", "cloud"];

    /// <summary>Gets or sets whether the manager reports that its head receives webhooks.</summary>
    public bool SupportsWebhooks { get; set; } = true;

    /// <summary>Gets or sets the sentence the manager reports for the page.</summary>
    public string PageNote { get; set; } = string.Empty;

    /// <summary>Gets every call that reached the manager, as <c>Operation</c> or <c>Operation:argument</c>, in order.</summary>
    internal List<string> Calls { get; } = [];

    /// <summary>Gets every save that reached the manager, as the identifier (null for a create) and the draft.</summary>
    internal List<(string? Id, AutomationDraft Draft)> Saves { get; } = [];

    /// <summary>Gets every cron preview that was asked for, as the expression, the zone and the count.</summary>
    internal List<(string Expression, string Zone, int Count)> Previews { get; } = [];

    /// <summary>Gets or sets the secret a save returns for an automation that has a webhook trigger and no secret yet, or null for none.</summary>
    internal string? SecretOnSave { get; set; }

    /// <summary>Gets or sets the secret a rotation returns.</summary>
    internal string RotatedSecret { get; set; } = "rotated-secret-canary";

    /// <summary>Gets or sets the run a run now request reports.</summary>
    internal HostedRunEntry NextRun { get; set; } = AutomationFixtures.Run("run-manual", HostedRunStatus.Succeeded, "manual");

    /// <summary>Gets or sets what a cron preview returns. The default previews two runs for any expression.</summary>
    internal Func<string, AutomationCronPreview> PreviewResult { get; set; } = static _ => new AutomationCronPreview(
        [new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero)],
        string.Empty);

    /// <summary>Gets the number of calls of an operation.</summary>
    /// <param name="operation">The operation name, for example <c>List</c>.</param>
    /// <returns>How many calls reached the manager.</returns>
    internal int CountOf(string operation) =>
        Calls.Count(call => string.Equals(call, operation, StringComparison.Ordinal) || call.StartsWith(operation + ":", StringComparison.Ordinal));

    /// <summary>Gets the automations the manager holds now.</summary>
    /// <returns>A snapshot.</returns>
    internal IReadOnlyList<HostedAutomationEntry> Snapshot() => [.. _entries];

    /// <summary>Makes an operation fail with an exception from now on.</summary>
    /// <param name="operation">The operation name: List, Save, SetEnabled, Delete, RunNow, History, Preview or Rotate.</param>
    /// <param name="failure">The exception, or null to make the operation succeed again.</param>
    internal void Fail(string operation, Exception? failure)
    {
        if (failure is null)
        {
            _failures.Remove(operation);
        }
        else
        {
            _failures[operation] = failure;
        }
    }

    /// <summary>Holds an operation open until <see cref="Release"/> is called.</summary>
    /// <param name="operation">The operation name.</param>
    internal void Hold(string operation) =>
        _gates[operation] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets a held operation complete.</summary>
    /// <param name="operation">The operation name.</param>
    internal void Release(string operation)
    {
        if (_gates.Remove(operation, out var gate))
        {
            gate.TrySetResult();
        }
    }

    /// <summary>Sets the run history of an automation, newest first.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="runs">The runs.</param>
    internal void SetHistory(string id, params HostedRunEntry[] runs) => _runs[id] = [.. runs];

    /// <inheritdoc />
    public async Task<IReadOnlyList<HostedAutomationEntry>> ListAsync(CancellationToken cancellationToken)
    {
        Calls.Add("List");
        await EnterAsync("List", cancellationToken).ConfigureAwait(true);
        return [.. _entries];
    }

    /// <inheritdoc />
    public async Task<AutomationSaveResult> SaveAsync(string? id, AutomationDraft draft, CancellationToken cancellationToken)
    {
        Calls.Add("Save:" + (id ?? "new"));
        Saves.Add((id, draft));
        await EnterAsync("Save", cancellationToken).ConfigureAwait(true);

        var hasWebhook = draft.Triggers.Any(static trigger => string.Equals(trigger.Kind, "webhook", StringComparison.Ordinal));
        var existing = id is null ? null : _entries.Single(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        var identifier = id ?? "auto-" + (++_counter).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var needsSecret = hasWebhook && existing?.HasWebhookSecret != true;
        var entry = new HostedAutomationEntry(
            identifier,
            draft,
            NextRunAt: null,
            LastRun: existing?.LastRun,
            WebhookPath: hasWebhook ? "/webhooks/" + identifier : string.Empty,
            HasWebhookSecret: hasWebhook && (needsSecret ? SecretOnSave is not null : existing?.HasWebhookSecret == true));
        if (existing is null)
        {
            _entries.Add(entry);
        }
        else
        {
            _entries[_entries.IndexOf(existing)] = entry;
        }

        return new AutomationSaveResult(entry, needsSecret ? SecretOnSave : null);
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        Calls.Add("SetEnabled:" + id + ":" + (enabled ? "on" : "off"));
        await EnterAsync("SetEnabled", cancellationToken).ConfigureAwait(true);
        var index = _entries.FindIndex(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        _entries[index] = _entries[index] with { Draft = _entries[index].Draft with { Enabled = enabled } };
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        Calls.Add("Delete:" + id);
        await EnterAsync("Delete", cancellationToken).ConfigureAwait(true);
        _entries.RemoveAll(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public async Task<HostedRunEntry> RunNowAsync(string id, CancellationToken cancellationToken)
    {
        Calls.Add("RunNow:" + id);
        await EnterAsync("RunNow", cancellationToken).ConfigureAwait(true);
        if (!_runs.TryGetValue(id, out var runs))
        {
            runs = [];
            _runs[id] = runs;
        }

        runs.Insert(0, NextRun);
        var index = _entries.FindIndex(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        _entries[index] = _entries[index] with { LastRun = NextRun };
        return NextRun;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HostedRunEntry>> HistoryAsync(string id, CancellationToken cancellationToken)
    {
        Calls.Add("History:" + id);
        await EnterAsync("History", cancellationToken).ConfigureAwait(true);
        return _runs.TryGetValue(id, out var runs) ? [.. runs] : [];
    }

    /// <inheritdoc />
    public async Task<AutomationCronPreview> PreviewCronAsync(string expression, string timeZone, int count, CancellationToken cancellationToken)
    {
        Calls.Add("Preview:" + expression);
        Previews.Add((expression, timeZone, count));
        await EnterAsync("Preview", cancellationToken).ConfigureAwait(true);
        return PreviewResult(expression);
    }

    /// <inheritdoc />
    public async Task<string> RotateWebhookSecretAsync(string id, CancellationToken cancellationToken)
    {
        Calls.Add("Rotate:" + id);
        await EnterAsync("Rotate", cancellationToken).ConfigureAwait(true);
        return RotatedSecret;
    }

    private async Task EnterAsync(string operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_gates.TryGetValue(operation, out var gate))
        {
            try
            {
                await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
        }

        if (_failures.TryGetValue(operation, out var failure))
        {
            throw failure;
        }
    }
}
