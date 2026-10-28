using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// The Context tab: the Kanban task, workflow phase, shared memory and lessons of the selected session, loaded from an
/// <see cref="ITaskContextSource"/>. It loads on selection and on an explicit refresh, cancels the previous load when the selection
/// changes, and drops the answer of any load that is no longer current.
/// </summary>
/// <remarks>
/// <para>
/// States: <see cref="TaskContextState.Loading"/> while a load runs; <see cref="TaskContextState.Ready"/> when the source answered without
/// warnings; <see cref="TaskContextState.Partial"/> when it answered with warnings; <see cref="TaskContextState.Unavailable"/> when the
/// agent has no task or the source threw or was cancelled. A fault never reaches the caller and its message is never shown, because a
/// source's exception text is not vetted for the panel.
/// </para>
/// <para>
/// Text: notes and warnings are untrusted plain text. Control and bidirectional characters are removed, each note is capped at
/// <see cref="MaxNoteChars"/> characters, at most <see cref="MaxNotes"/> notes of each list are kept and <see cref="NotesShown"/> are shown.
/// </para>
/// <para>Not thread-safe: it runs on the UI context that owns the presentation models.</para>
/// </remarks>
public sealed class TaskContextModel : ObservableModel, IDisposable
{
    /// <summary>The most notes kept in each list.</summary>
    public const int MaxNotes = 20;

    /// <summary>The most characters kept of a note.</summary>
    public const int MaxNoteChars = 500;

    /// <summary>The characters of a note the tab draws before the ellipsis.</summary>
    public const int NoteDisplayChars = 280;

    /// <summary>The notes of each list the tab draws.</summary>
    public const int NotesShown = 8;

    /// <summary>The most warnings kept.</summary>
    public const int MaxWarnings = 20;

    private static readonly string[] ViewProperties =
    [
        nameof(State),
        nameof(Notice),
        nameof(Heading),
        nameof(Body),
        nameof(HasCard),
        nameof(Title),
        nameof(TaskId),
        nameof(KanbanStatus),
        nameof(GraphId),
        nameof(Phase),
        nameof(WorkflowText),
        nameof(SharedMemory),
        nameof(Lessons),
        nameof(Warnings),
        nameof(WarningsHeader),
    ];

    private readonly ITaskContextSource? _source;
    private readonly TimeProvider _timeProvider;
    private readonly AgentPanelCommand _refreshCommand;
    private StageObservationScope? _scope;
    private CancellationTokenSource? _running;
    private long _generation;
    private bool _disposed;
    private bool? _substrateAvailable;
    private ContextView _view;

    /// <summary>Creates a model with no selection.</summary>
    /// <param name="source">The source of the context. Null means there is none, and every selection shows <see cref="TaskContextNotice.NoTask"/>.</param>
    /// <param name="timeProvider">The clock of the relative times of notes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public TaskContextModel(ITaskContextSource? source, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _source = source;
        _timeProvider = timeProvider;
        _view = ContextView.NoTask();
        _refreshCommand = new AgentPanelCommand(_ => RefreshAsync(), static _ => { });
    }

    /// <summary>Gets the load state.</summary>
    public TaskContextState State => _view.State;

    /// <summary>Gets which heading and body the tab shows.</summary>
    public TaskContextNotice Notice => EffectiveNotice;

    /// <summary>Gets the heading of the notice, or null when there is none.</summary>
    public string? Heading => EffectiveNotice switch
    {
        TaskContextNotice.Loading => AgentPanelCopy.ContextLoadingHeading,
        TaskContextNotice.NoTask => AgentPanelCopy.ContextNoTaskHeading,
        TaskContextNotice.SubstrateLimited => AgentPanelCopy.ContextLimitedHeading,
        TaskContextNotice.Failed => AgentPanelCopy.ContextUnavailableHeading,
        _ => null,
    };

    /// <summary>Gets the body of the notice, or null when there is none.</summary>
    public string? Body => EffectiveNotice switch
    {
        TaskContextNotice.NoTask => AgentPanelCopy.ContextNoTaskBody,
        TaskContextNotice.SubstrateLimited => AgentPanelCopy.ContextLimitedBody,
        TaskContextNotice.Failed => _view.FailureBody ?? AgentPanelCopy.ContextUnavailableBody,
        TaskContextNotice.Partial => AgentPanelCopy.ContextPartialBody,
        _ => null,
    };

    /// <summary>Gets whether the task card and lists are shown: the context loaded, with or without warnings.</summary>
    public bool HasCard => _view.State is TaskContextState.Ready or TaskContextState.Partial;

    /// <summary>Gets the task title, or an empty string when there is no card.</summary>
    public string Title => _view.Title;

    /// <summary>Gets the task identifier, or null.</summary>
    public string? TaskId => _view.TaskId;

    /// <summary>Gets the Kanban status of the task as plain text, or null when unknown.</summary>
    public string? KanbanStatus => _view.KanbanStatus;

    /// <summary>Gets the Graph identifier the task is registered under, or null.</summary>
    public string? GraphId => _view.GraphId;

    /// <summary>Gets the workflow phase, or null when it could not be read.</summary>
    public GsdPhaseInfo? Phase => _view.Phase;

    /// <summary>Gets the workflow line: <c>Phase {Number} · {Name}, plan {c} of {t}</c> or <c>Phase unknown</c>.</summary>
    public string WorkflowText => _view.WorkflowText;

    /// <summary>Gets the shared memory list.</summary>
    public ContextNoteList SharedMemory => _view.SharedMemory;

    /// <summary>Gets the lessons list.</summary>
    public ContextNoteList Lessons => _view.Lessons;

    /// <summary>Gets the plain-text warnings, at most <see cref="MaxWarnings"/>.</summary>
    public IReadOnlyList<string> Warnings => _view.Warnings;

    /// <summary>Gets the header of the warnings block, or null when there is no warning.</summary>
    public string? WarningsHeader => _view.Warnings.Count == 0 ? null : AgentPanelCopy.ContextWarningsHeader;

    /// <summary>
    /// Gets or sets whether Substrate is reachable: true or false when the pipeline knows, null while it does not. While false, a loaded
    /// context carries the <see cref="TaskContextNotice.SubstrateLimited"/> notice. Setting it never reloads the context.
    /// </summary>
    public bool? SubstrateAvailable
    {
        get => _substrateAvailable;
        set
        {
            if (_substrateAvailable == value)
            {
                return;
            }

            _substrateAvailable = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(Notice));
            NotifyPropertyChanged(nameof(Heading));
            NotifyPropertyChanged(nameof(Body));
        }
    }

    /// <summary>Gets the command that reloads the context of the current selection.</summary>
    public AgentPanelCommand RefreshCommand => _refreshCommand;

    private TaskContextNotice EffectiveNotice =>
        _view.State is TaskContextState.Ready or TaskContextState.Partial && _substrateAvailable == false
            ? TaskContextNotice.SubstrateLimited
            : _view.Notice;

    /// <summary>Selects a session and loads its context, cancelling the load of the previous selection.</summary>
    /// <param name="scope">The session to describe, or null when no session is selected.</param>
    /// <returns>A task that completes when the load has ended or was superseded. It never faults.</returns>
    public Task SelectAsync(StageObservationScope? scope)
    {
        _scope = scope;
        return LoadAsync();
    }

    /// <summary>Reloads the context of the current selection.</summary>
    /// <returns>A task that completes when the load has ended or was superseded. It never faults.</returns>
    public Task RefreshAsync() => LoadAsync();

    /// <summary>Cancels the running load, if any. A load that was cancelled this way leaves the tab <see cref="TaskContextState.Unavailable"/>.</summary>
    public void Cancel()
    {
        if (_running is null)
        {
            return;
        }

        _generation++;
        CancelRunning();
        Show(ContextView.Failed(null));
    }

    /// <summary>Cancels the running load. The model shows nothing afterwards.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _generation++;
        CancelRunning();
        _running?.Dispose();
    }

    private void CancelRunning() => _running?.Cancel();

    private async Task LoadAsync()
    {
        if (_disposed)
        {
            return;
        }

        var generation = ++_generation;
        CancelRunning();
        var scope = _scope;
        if (scope is null || _source is null)
        {
            Show(ContextView.NoTask());
            return;
        }

        Show(ContextView.Loading());
        using var cancellation = new CancellationTokenSource();
        _running = cancellation;
        try
        {
            var context = await _source.GetAsync(scope, cancellation.Token).ConfigureAwait(true);
            if (generation == _generation)
            {
                Show(ContextView.From(context, _timeProvider.GetUtcNow()));
            }
        }
        catch (OperationCanceledException)
        {
            ShowFailureIfCurrent(generation);
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            ShowFailureIfCurrent(generation);
        }
        finally
        {
            if (ReferenceEquals(_running, cancellation))
            {
                _running = null;
            }
        }
    }

    private void ShowFailureIfCurrent(long generation)
    {
        if (generation == _generation)
        {
            Show(ContextView.Failed(null));
        }
    }

    private void Show(ContextView view)
    {
        _view = view;
        foreach (var property in ViewProperties)
        {
            NotifyPropertyChanged(property);
        }
    }

    /// <summary>The content of the tab for one state. A new view replaces the previous one as a whole.</summary>
    private sealed record ContextView
    {
        private static readonly ContextNoteList NoMemory = ContextNoteList.Build("Shared memory", AgentPanelCopy.MemoryEmpty, [], default);
        private static readonly ContextNoteList NoLessons = ContextNoteList.Build("Lessons", AgentPanelCopy.LessonsEmpty, [], default);

        internal required TaskContextState State { get; init; }

        internal required TaskContextNotice Notice { get; init; }

        internal string? FailureBody { get; init; }

        internal string Title { get; init; } = string.Empty;

        internal string? TaskId { get; init; }

        internal string? KanbanStatus { get; init; }

        internal string? GraphId { get; init; }

        internal GsdPhaseInfo? Phase { get; init; }

        internal string WorkflowText { get; init; } = AgentPanelCopy.PhaseUnknown;

        internal ContextNoteList SharedMemory { get; init; } = NoMemory;

        internal ContextNoteList Lessons { get; init; } = NoLessons;

        internal List<string> Warnings { get; init; } = [];

        internal static ContextView Loading() =>
            new() { State = TaskContextState.Loading, Notice = TaskContextNotice.Loading };

        internal static ContextView NoTask() =>
            new() { State = TaskContextState.Unavailable, Notice = TaskContextNotice.NoTask };

        internal static ContextView Failed(string? body) =>
            new() { State = TaskContextState.Unavailable, Notice = TaskContextNotice.Failed, FailureBody = body };

        internal static ContextView From(TaskContext context, DateTimeOffset now)
        {
            var warnings = context.Warnings
                .Select(warning => PanelText.Plain(warning, 280))
                .Where(warning => warning.Length > 0)
                .Take(MaxWarnings)
                .ToList();
            var taskId = PanelText.Plain(context.TaskId, 80);
            var kanban = PanelText.Plain(context.KanbanStatus, 80);
            var hasContent = taskId.Length > 0 || kanban.Length > 0 || context.Phase is not null
                || context.SharedMemory.Count > 0 || context.Lessons.Count > 0;
            if (!hasContent)
            {
                return warnings.Count == 0 ? NoTask() : Failed(warnings[0]);
            }

            var title = PanelText.Plain(context.Title, 200);
            var graphId = PanelText.Plain(context.GraphId, 64);
            return new ContextView
            {
                State = warnings.Count == 0 ? TaskContextState.Ready : TaskContextState.Partial,
                Notice = warnings.Count == 0 ? TaskContextNotice.None : TaskContextNotice.Partial,
                Title = title.Length == 0 ? AgentPanelCopy.UntitledTask : title,
                TaskId = taskId.Length == 0 ? null : taskId,
                KanbanStatus = kanban.Length == 0 ? null : kanban,
                GraphId = graphId.Length == 0 ? null : graphId,
                Phase = context.Phase,
                WorkflowText = WorkflowOf(context.Phase),
                SharedMemory = ContextNoteList.Build("Shared memory", AgentPanelCopy.MemoryEmpty, context.SharedMemory, now),
                Lessons = ContextNoteList.Build("Lessons", AgentPanelCopy.LessonsEmpty, context.Lessons, now),
                Warnings = warnings,
            };
        }

        private static string WorkflowOf(GsdPhaseInfo? phase) => phase is null
            ? AgentPanelCopy.PhaseUnknown
            : $"Phase {PanelText.Plain(phase.Number, 20)} \u00B7 {PanelText.Plain(phase.Name, 80)}, plan {phase.PlansComplete} of {phase.PlansTotal}";
    }
}
