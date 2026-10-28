using System.Text.Json;
using OmpOS.Contracts.Hooks;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Hooks;

/// <summary>
/// The bounded execution log of the Hooks page and of the log drawer in a thread: the newest runs first, filterable by outcome, with every text
/// redacted, and copyable as JSON. It keeps the latest <see cref="Capacity"/> runs; older ones are dropped. It starts no hook.
/// </summary>
public sealed class HookRunLogModel : ObservableModel
{
    /// <summary>The number of runs the log keeps.</summary>
    public const int Capacity = 500;

    private static readonly HookRunOutcome[] Outcomes = Enum.GetValues<HookRunOutcome>();

    private readonly TimeZoneInfo _zone;
    private readonly List<HookRunItem> _runs = [];
    private List<HookLogRow> _rows = [];
    private HookRunOutcome? _filter;

    /// <summary>Initializes a new instance of the <see cref="HookRunLogModel"/> class.</summary>
    /// <param name="zone">The time zone times are shown in, or null for the local zone.</param>
    public HookRunLogModel(TimeZoneInfo? zone = null)
    {
        _zone = zone ?? TimeZoneInfo.Local;
    }

    /// <summary>Gets the entries of the filter list: every run, then one entry per outcome. <see cref="SelectedFilterIndex"/> indexes it.</summary>
    public IReadOnlyList<string> FilterChoices { get; } = [HooksCopy.RunLogAll, .. Outcomes.Select(HooksCopy.OutcomeText)];

    /// <summary>Gets or sets the index of the selected entry of <see cref="FilterChoices"/>; zero shows every run. A value out of range shows every run.</summary>
    public int SelectedFilterIndex
    {
        get => _filter is null ? 0 : Array.IndexOf(Outcomes, _filter.Value) + 1;
        set => SetFilter(value > 0 && value <= Outcomes.Length ? Outcomes[value - 1] : null);
    }

    /// <summary>Gets the rows that pass the filter, newest first. Replaced as a whole when anything changes.</summary>
    public IReadOnlyList<HookLogRow> Rows => _rows;

    /// <summary>Gets the number of runs the log holds, whatever the filter.</summary>
    public int TotalCount => _runs.Count;

    /// <summary>Gets the outcome the log is filtered to, or null for every run.</summary>
    public HookRunOutcome? Filter => _filter;

    /// <summary>Gets a value indicating whether the log holds no run at all.</summary>
    public bool IsEmpty => _runs.Count == 0;

    /// <summary>Gets a value indicating whether runs exist but the filter hides all of them.</summary>
    public bool IsFilteredEmpty => _runs.Count > 0 && _rows.Count == 0;

    /// <summary>Gets the text to show when there is nothing to list: that no hook ran, that the filter hides everything, or an empty string.</summary>
    public string EmptyText => IsEmpty ? HooksCopy.RunLogEmpty : IsFilteredEmpty ? HooksCopy.RunLogNoMatch : string.Empty;

    /// <summary>Replaces the log's content.</summary>
    /// <param name="runs">The runs, oldest first. Only the newest <see cref="Capacity"/> are kept.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runs"/> is null.</exception>
    public void Replace(IEnumerable<HookRunItem> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        _runs.Clear();
        _runs.AddRange(runs);
        Trim();
        Rebuild();
    }

    /// <summary>Adds one run, the newest.</summary>
    /// <param name="run">The run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="run"/> is null.</exception>
    public void Append(HookRunItem run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _runs.Add(run);
        Trim();
        Rebuild();
    }

    /// <summary>Filters the log to one outcome, or to every run.</summary>
    /// <param name="outcome">The outcome, or null for every run.</param>
    public void SetFilter(HookRunOutcome? outcome)
    {
        if (_filter == outcome)
        {
            return;
        }

        _filter = outcome;
        Rebuild();
    }

    /// <summary>
    /// Renders the rows that pass the filter as a JSON array, newest first, for the clipboard. Every text field is redacted and the input of a hook
    /// is never part of it (only its digest exists, and the log does not keep that).
    /// </summary>
    /// <returns>The JSON text.</returns>
    public string ToJson()
    {
        var events = _rows.Select(row => ToEvent(row)).ToArray();
        return JsonSerializer.Serialize(events, HooksContractsJsonContext.Default.HookRunEventArray);
    }

    private static HookRunEvent ToEvent(HookLogRow row) => new()
    {
        At = row.Run.At,
        SessionId = row.Run.SessionId,
        EventName = row.Run.EventName,
        HookId = row.Run.HookId,
        Scope = ScopeName(row.Run.Scope),
        Status = OutcomeName(row.Run.Outcome),
        Decision = row.Run.Decision,
        ExitCode = row.Run.ExitCode,
        DurationMs = row.Run.DurationMs,
        MatchedValue = row.Run.MatchedValue,
        UserMessage = row.UserMessage.Length == 0 ? null : row.UserMessage,
        StandardOutputExcerpt = row.Output.Length == 0 ? null : row.Output,
        StandardErrorExcerpt = row.ErrorOutput.Length == 0 ? null : row.ErrorOutput,
        OutputTruncated = row.Run.OutputTruncated,
        Note = row.Run.Note,
    };

    private static string ScopeName(HookScopeKind scope) => scope switch
    {
        HookScopeKind.Managed => "managed",
        HookScopeKind.Project => "project",
        HookScopeKind.User => "user",
        HookScopeKind.ClaudeLocal => "claudeLocal",
        HookScopeKind.ClaudeProject => "claudeProject",
        HookScopeKind.ClaudeUser => "claudeUser",
        _ => "plugin",
    };

    private static string OutcomeName(HookRunOutcome outcome) => outcome switch
    {
        HookRunOutcome.Succeeded => "succeeded",
        HookRunOutcome.Denied => "denied",
        HookRunOutcome.Failed => "failed",
        HookRunOutcome.TimedOut => "timedOut",
        HookRunOutcome.Skipped => "skipped",
        HookRunOutcome.Faulted => "faulted",
        HookRunOutcome.AwaitingConsent => "awaitingConsent",
        HookRunOutcome.Quarantined => "pluginQuarantined",
        _ => "invalidResponse",
    };

    private void Trim()
    {
        if (_runs.Count > Capacity)
        {
            _runs.RemoveRange(0, _runs.Count - Capacity);
        }
    }

    private void Rebuild()
    {
        var rows = new List<HookLogRow>(_runs.Count);
        for (var index = _runs.Count - 1; index >= 0; index--)
        {
            var run = _runs[index];
            if (_filter is null || run.Outcome == _filter)
            {
                rows.Add(HookLogRow.From(run, _zone));
            }
        }

        _rows = rows;
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(TotalCount));
        NotifyPropertyChanged(nameof(Filter));
        NotifyPropertyChanged(nameof(SelectedFilterIndex));
        NotifyPropertyChanged(nameof(IsEmpty));
        NotifyPropertyChanged(nameof(IsFilteredEmpty));
        NotifyPropertyChanged(nameof(EmptyText));
    }
}
