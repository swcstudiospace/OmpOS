using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Checkpoints;

/// <summary>One turn of the timeline, with the words a row shows and the accessible names of its controls.</summary>
public sealed class CheckpointRow : ObservableModel
{
    private string _timeText;

    /// <summary>Creates a row.</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="now">The current time, for the relative time text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="turn"/> is null.</exception>
    public CheckpointRow(CheckpointTurnInfo turn, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(turn);
        Turn = turn;
        _timeText = RelativeTimeText.Format(turn.RecordedAt, now);
    }

    /// <summary>Gets the turn.</summary>
    public CheckpointTurnInfo Turn { get; }

    /// <summary>Gets the turn's number in the timeline. It is the parameter of the row's commands and the number <c>/rewind</c> takes.</summary>
    public int TurnId => Turn.TurnId;

    /// <summary>Gets the turn's prompt, or an empty string.</summary>
    public string Label => Turn.Label;

    /// <summary>Gets the number of files the turn changed.</summary>
    public int FileCount => Turn.Files.Count;

    /// <summary>Gets the files the turn changed, as a short list for a tooltip.</summary>
    public string FilesText => string.Join(", ", Turn.Files);

    /// <summary>Gets the relative time of the turn, for example <c>2 min ago</c>.</summary>
    public string TimeText => _timeText;

    /// <summary>Gets the accessible name of the row.</summary>
    public string RowName => CheckpointsCopy.RowName(TurnId, Label, FileCount, _timeText);

    /// <summary>Gets the accessible name of the row's Preview control.</summary>
    public string PreviewName => CheckpointsCopy.PreviewName(TurnId);

    /// <summary>Gets the accessible name of the row's Rewind control.</summary>
    public string RewindName => CheckpointsCopy.RewindName(TurnId);

    /// <summary>Takes a newer relative time.</summary>
    /// <param name="now">The current time.</param>
    public void RefreshTime(DateTimeOffset now)
    {
        if (SetProperty(ref _timeText, RelativeTimeText.Format(Turn.RecordedAt, now), nameof(TimeText)))
        {
            NotifyPropertyChanged(nameof(RowName));
        }
    }
}
