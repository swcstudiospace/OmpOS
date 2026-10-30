using System.Globalization;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Kanban;

/// <summary>
/// The live side of one Kanban card: the current <see cref="KanbanCard"/> snapshot, the Substrate fields, and the steering entry point.
/// <see cref="KanbanBoardModel"/> creates one per session, keeps it for as long as the session is on the board and replaces its
/// <see cref="Card"/> when the card changes, so text typed into the steer box survives a status change.
/// </summary>
/// <remarks>Not thread-safe: it runs on the context that owns the presentation models.</remarks>
public sealed class KanbanCardViewModel : ObservableModel, IDisposable
{
    /// <summary>The text of a field Substrate did not report, or of every field when Substrate cannot be reached.</summary>
    public const string UnavailableText = "unavailable";

    private const int MaxFieldChars = 120;
    private const int MaxMessageChars = 200;

    private readonly ICardSteering? _steering;
    private readonly Action<string> _announce;
    private readonly Action<string> _open;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly AgentPanelCommand _steerCommand;
    private readonly AgentPanelCommand _openCommand;
    private KanbanCard _card;
    private KanbanCardDetails? _details;
    private string _steerText = string.Empty;
    private string? _steerResultText;
    private bool _isSteering;
    private bool _disposed;

    /// <summary>Creates the view model of one card.</summary>
    /// <param name="card">The first snapshot of the card.</param>
    /// <param name="steering">The steering service, or null when the head has none.</param>
    /// <param name="announce">Receives the text the board announces to assistive technology.</param>
    /// <param name="open">Receives the session identifier when the card is activated.</param>
    internal KanbanCardViewModel(KanbanCard card, ICardSteering? steering, Action<string> announce, Action<string> open)
    {
        _card = card;
        _steering = steering;
        _announce = announce;
        _open = open;
        _steerCommand = new AgentPanelCommand(
            parameter => SteerAsync(parameter is CardSteerMode mode ? mode : CardSteerMode.SendNow),
            OnSteerFault,
            _ => _steering is not null && !_disposed && !_isSteering && _steerText.Trim().Length > 0);
        _openCommand = new AgentPanelCommand(
            _ =>
            {
                _open(_card.Id);
                return Task.CompletedTask;
            },
            static _ => { });
    }

    /// <summary>Gets the current snapshot of the card. Replaced as a whole when anything on the card changes.</summary>
    public KanbanCard Card => _card;

    /// <summary>Gets the session identifier.</summary>
    public string Id => _card.Id;

    /// <summary>Gets what Substrate reported for the session, or null when nothing was read, Substrate cannot be reached, or it reported nothing.</summary>
    public KanbanCardDetails? Details => _details;

    /// <summary>Gets whether Substrate reported details for the session.</summary>
    public bool IsSubstrateAvailable => _details is { Available: true };

    /// <summary>Gets <c>Task: {title}</c>, or <c>Task: unavailable</c>.</summary>
    public string TaskText => FieldText("Task", _details?.TaskTitle);

    /// <summary>Gets <c>Graph: {id}</c>, or <c>Graph: unavailable</c>.</summary>
    public string GraphText => FieldText("Graph", _details?.GraphId);

    /// <summary>Gets <c>Lessons: {n}</c>, or <c>Lessons: unavailable</c>.</summary>
    public string LessonsText => FieldText("Lessons", _details is { Available: true, LessonsCount: { } count } ? count.ToString(CultureInfo.InvariantCulture) : null);

    /// <summary>Gets <c>Lease: {holder}</c>, or <c>Lease: unavailable</c>.</summary>
    public string LeaseText => FieldText("Lease", _details?.LeaseHolder);

    /// <summary>Gets the accessible name of the Substrate fields row: the four texts in order.</summary>
    public string SubstrateName => $"Substrate, {TaskText}, {GraphText}, {LessonsText}, {LeaseText}";

    /// <summary>Gets whether the card can send a message: the head gave the board a steering service.</summary>
    public bool CanSteer => _steering is not null;

    /// <summary>Gets or sets the message typed into the steer box. A failed send leaves it; an accepted send clears it.</summary>
    public string SteerText
    {
        get => _steerText;
        set
        {
            var next = value ?? string.Empty;
            if (SetProperty(ref _steerText, next))
            {
                _steerCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets the result of the last steering attempt in words, or null before the first one. The view announces it as a live region.</summary>
    public string? SteerResultText => _steerResultText;

    /// <summary>Gets whether the last steering attempt was refused, which the view draws as an error.</summary>
    public bool SteerFailed { get; private set; }

    /// <summary>Gets whether a send is in progress. The send commands are disabled meanwhile.</summary>
    public bool IsSteering => _isSteering;

    /// <summary>
    /// Gets the command that sends <see cref="SteerText"/> to the session through the steering service. Its parameter is a
    /// <see cref="CardSteerMode"/>; null means <see cref="CardSteerMode.SendNow"/>. It is disabled while the text is blank, while a send runs, and when there is no service.
    /// </summary>
    public AgentPanelCommand SteerCommand => _steerCommand;

    /// <summary>Gets the command that opens or focuses the session of the card: it raises the board's <see cref="KanbanBoardModel.OpenSessionRequested"/>.</summary>
    public AgentPanelCommand OpenCommand => _openCommand;

    /// <summary>Reads the Substrate fields of the session from a source.</summary>
    /// <param name="source">The source, or null when the head has none: the fields then read unavailable.</param>
    /// <param name="cancellationToken">Cancels the read; the fields then stay as they were.</param>
    /// <returns>A task that completes when the fields are set.</returns>
    public async Task LoadDetailsAsync(IKanbanCardDetailsSource? source, CancellationToken cancellationToken)
    {
        KanbanCardDetails? loaded = null;
        if (source is not null)
        {
            try
            {
                loaded = await source.LoadAsync(_card.Id, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception fault) when (fault is not OperationCanceledException and not OutOfMemoryException)
            {
                loaded = null;
            }
        }

        SetDetails(loaded is { Available: true } ? Plain(loaded) : null);
    }

    /// <summary>Cancels a send in progress.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    /// <summary>Replaces the snapshot of the card.</summary>
    /// <param name="card">The new snapshot.</param>
    internal void Update(KanbanCard card)
    {
        if (_card.Equals(card))
        {
            return;
        }

        _card = card;
        NotifyPropertyChanged(nameof(Card));
        NotifyPropertyChanged(nameof(Id));
    }

    private static KanbanCardDetails Plain(KanbanCardDetails details) => details with
    {
        TaskTitle = Known(details.TaskTitle),
        GraphId = Known(details.GraphId),
        LeaseHolder = Known(details.LeaseHolder),
        LessonsCount = details.LessonsCount is { } count && count >= 0 ? count : null,
    };

    private static string? Known(string? value)
    {
        var plain = PanelText.Plain(value, MaxFieldChars);
        return plain.Length == 0 ? null : plain;
    }

    private static string FieldText(string label, string? value) => $"{label}: {value ?? UnavailableText}";

    private void SetDetails(KanbanCardDetails? details)
    {
        if (Equals(_details, details))
        {
            return;
        }

        _details = details;
        NotifyPropertyChanged(nameof(Details));
        NotifyPropertyChanged(nameof(IsSubstrateAvailable));
        NotifyPropertyChanged(nameof(TaskText));
        NotifyPropertyChanged(nameof(GraphText));
        NotifyPropertyChanged(nameof(LessonsText));
        NotifyPropertyChanged(nameof(LeaseText));
        NotifyPropertyChanged(nameof(SubstrateName));
    }

    private async Task SteerAsync(CardSteerMode mode)
    {
        var text = _steerText.Trim();
        if (_steering is null || _disposed || text.Length == 0)
        {
            return;
        }

        SetSteering(true);
        CardSteerResult result;
        try
        {
            result = await _steering.SteerAsync(_card.Id, text, mode, _lifetime.Token).ConfigureAwait(true);
        }
        catch (Exception fault) when (fault is not OperationCanceledException and not OutOfMemoryException)
        {
            result = CardSteerResult.Failed(FaultText(fault));
        }
        finally
        {
            SetSteering(false);
        }

        Report(result);
    }

    private void OnSteerFault(Exception fault) => Report(CardSteerResult.Failed(FaultText(fault)));

    private static string FaultText(Exception fault)
    {
        var reason = PanelText.Plain(fault.Message, MaxMessageChars);
        return reason.Length == 0 ? "The message could not be sent" : reason;
    }

    private void Report(CardSteerResult result)
    {
        var message = PanelText.Plain(result.Message, MaxMessageChars);
        if (message.Length == 0)
        {
            message = result.Outcome switch
            {
                CardSteerOutcome.Delivered => "Sent to the session",
                CardSteerOutcome.Queued => "Queued for the next turn",
                _ => "The message could not be sent",
            };
        }

        if (result.IsAccepted)
        {
            SteerText = string.Empty;
        }

        SteerFailed = !result.IsAccepted;
        NotifyPropertyChanged(nameof(SteerFailed));
        _steerResultText = message;
        NotifyPropertyChanged(nameof(SteerResultText));
        _announce($"{_card.Title}: {message}");
    }

    private void SetSteering(bool value)
    {
        if (SetProperty(ref _isSteering, value, nameof(IsSteering)))
        {
            _steerCommand.NotifyCanExecuteChanged();
        }
    }
}
