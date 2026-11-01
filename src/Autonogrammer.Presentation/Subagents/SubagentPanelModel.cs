using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Subagents;

/// <summary>
/// The sub-agent panel: one row per omp task call, with status, elapsed time from an injected <see cref="TimeProvider"/>
/// and the number running. Output is stored as plain text. The view is limited to ACP.
/// </summary>
/// <remarks>Not thread-safe: it runs on the UI context that owns the presentation models.</remarks>
public sealed class SubagentPanelModel : ObservableModel
{
    private readonly TimeProvider _time;
    private readonly List<Row> _rows = [];
    private readonly Dictionary<string, DateTimeOffset> _started = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TimeSpan> _frozen = new(StringComparer.Ordinal);
    private int _unnamed;
    private ISubagentActivitySource? _source;
    private IReadOnlyList<SubagentRowModel> _published = [];
    private int _runningCount;
    private bool _namesUnavailable;
    private string _liveAnnouncement = string.Empty;

    /// <summary>Creates a panel that reads elapsed time from <paramref name="time"/> and never from the wall clock.</summary>
    /// <param name="time">The clock. Tests inject a manual provider.</param>
    public SubagentPanelModel(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>Gets the notice that this view reads the ACP stream only.</summary>
    public string AcpOnlyNotice { get; } = SubagentCopy.AcpOnlyNotice;

    /// <summary>Gets whether the view is limited to ACP. Always true: the RPC source is deferred.</summary>
    public bool LimitedToAcp { get; } = true;

    /// <summary>Gets the rows, in the order the task calls started.</summary>
    public IReadOnlyList<SubagentRowModel> Rows => _published;

    /// <summary>Gets how many rows are running.</summary>
    public int RunningCount => _runningCount;

    /// <summary>Gets the running count as screen text.</summary>
    public string RunningCountText => $"{_runningCount} running";

    /// <summary>Gets whether any row had to fall back to <c>Sub-agent N</c> because the call did not name its agent.</summary>
    public bool SubagentNamesUnavailable => _namesUnavailable;

    /// <summary>Gets the latest status sentence for the live region. Empty until a status changes.</summary>
    public string LiveAnnouncement => _liveAnnouncement;

    /// <summary>Subscribes to <paramref name="source"/> and applies each update. A later call replaces the subscription.</summary>
    /// <param name="source">The activity source. Not null.</param>
    public void Attach(ISubagentActivitySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_source is not null)
        {
            _source.Updated -= OnUpdated;
        }

        _source = source;
        source.Updated += OnUpdated;
    }

    /// <summary>Applies one recorded or live update.</summary>
    /// <param name="update">The update. A blank call id is ignored.</param>
    public void Apply(SubagentActivityUpdateEventArgs update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (string.IsNullOrWhiteSpace(update.CallId))
        {
            return;
        }

        var index = IndexOf(update.CallId);
        var now = _time.GetUtcNow();
        if (index < 0)
        {
            var named = !string.IsNullOrWhiteSpace(update.AgentName);
            var name = named ? PanelText.Plain(update.AgentName, 80) : SubagentCopy.Unnamed(++_unnamed);
            if (name.Length == 0)
            {
                name = SubagentCopy.Unnamed(_unnamed == 0 ? ++_unnamed : _unnamed);
                named = false;
            }

            _started[update.CallId] = now;
            var status = update.Status ?? SubagentStatus.Running;
            if (status != SubagentStatus.Running)
            {
                _frozen[update.CallId] = TimeSpan.Zero;
            }

            _rows.Add(new Row(update.CallId, name, named, status, LastLine(update.OutputLine)));
            Publish(update.CallId, statusChanged: true);
            return;
        }

        var current = _rows[index];
        var nameFromCall = current.NameFromCall;
        var nameText = current.Name;
        if (!string.IsNullOrWhiteSpace(update.AgentName))
        {
            var plain = PanelText.Plain(update.AgentName, 80);
            if (plain.Length > 0)
            {
                nameText = plain;
                nameFromCall = true;
            }
        }

        var nextStatus = update.Status ?? current.Status;
        if (current.Status == SubagentStatus.Running && nextStatus != SubagentStatus.Running)
        {
            _frozen[update.CallId] = ElapsedOf(update.CallId, now);
        }

        var line = update.OutputLine is null ? current.LastLine : LastLine(update.OutputLine);
        _rows[index] = current with { Name = nameText, NameFromCall = nameFromCall, Status = nextStatus, LastLine = line };
        Publish(update.CallId, statusChanged: nextStatus != current.Status);
    }

    /// <summary>Recomputes elapsed time for running rows from the injected clock. Terminal rows stay frozen.</summary>
    public void RefreshElapsed() => Publish(changedCallId: null, statusChanged: false);

    private void OnUpdated(object? sender, SubagentActivityUpdateEventArgs update) => Apply(update);

    private void Publish(string? changedCallId, bool statusChanged)
    {
        var now = _time.GetUtcNow();
        var published = new SubagentRowModel[_rows.Count];
        var running = 0;
        var unnamed = false;
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (row.Status == SubagentStatus.Running)
            {
                running++;
            }

            if (!row.NameFromCall)
            {
                unnamed = true;
            }

            published[i] = new SubagentRowModel
            {
                CallId = row.CallId,
                Name = row.Name,
                NameFromCall = row.NameFromCall,
                Status = row.Status,
                Elapsed = ElapsedOf(row.CallId, now),
                LastLine = row.LastLine,
            };
        }

        _published = published;
        SetProperty(ref _runningCount, running, nameof(RunningCount));
        SetProperty(ref _namesUnavailable, unnamed, nameof(SubagentNamesUnavailable));
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(RunningCountText));
        if (statusChanged && changedCallId is not null)
        {
            var row = published.First(item => string.Equals(item.CallId, changedCallId, StringComparison.Ordinal));
            SetProperty(ref _liveAnnouncement, $"{row.Name} {row.StatusLabel}", nameof(LiveAnnouncement));
        }
    }

    private TimeSpan ElapsedOf(string callId, DateTimeOffset now)
    {
        if (_frozen.TryGetValue(callId, out var frozen))
        {
            return frozen;
        }

        return _started.TryGetValue(callId, out var started) ? now - started : TimeSpan.Zero;
    }

    private int IndexOf(string callId)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (string.Equals(_rows[i].CallId, callId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static string LastLine(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return string.Empty;
        }

        var last = string.Empty;
        foreach (var line in output.Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                last = line.Trim();
            }
        }

        return PanelText.Plain(last, 240);
    }

    private sealed record Row(string CallId, string Name, bool NameFromCall, SubagentStatus Status, string LastLine);
}
