using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Queue;

/// <summary>One message waiting for the agent's turn to end.</summary>
public sealed class QueuedMessage : ObservableModel
{
    /// <summary>The most characters of a message shown in its row.</summary>
    public const int PreviewChars = 80;

    private string _text;
    private int _position;
    private bool _canMoveUp;
    private bool _canMoveDown;

    /// <summary>Creates a message.</summary>
    /// <param name="id">The message's identifier, unique in its queue.</param>
    /// <param name="text">The message.</param>
    /// <param name="queuedAt">When it was queued.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> or <paramref name="text"/> is null, empty or white space.</exception>
    public QueuedMessage(string id, string text, DateTimeOffset queuedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Id = id;
        _text = text;
        QueuedAt = queuedAt;
    }

    /// <summary>Gets the message's identifier. It is the parameter of the row's commands.</summary>
    public string Id { get; }

    /// <summary>Gets when the message was queued.</summary>
    public DateTimeOffset QueuedAt { get; }

    /// <summary>Gets the message text.</summary>
    public string Text => _text;

    /// <summary>Gets the message as one short line for its row.</summary>
    public string PreviewText
    {
        get
        {
            var flat = _text.ReplaceLineEndings(" ").Trim();
            return flat.Length <= PreviewChars ? flat : flat[..PreviewChars].TrimEnd() + "\u2026";
        }
    }

    /// <summary>Gets the message's place in the queue, from 1. The first message is the next to run.</summary>
    public int Position => _position;

    /// <summary>Gets whether the message may move up.</summary>
    public bool CanMoveUp => _canMoveUp;

    /// <summary>Gets whether the message may move down.</summary>
    public bool CanMoveDown => _canMoveDown;

    /// <summary>Gets the accessible name of the row.</summary>
    public string RowName => QueueCopy.RowName(_position, PreviewText);

    /// <summary>Gets the accessible name of the row's Remove control.</summary>
    public string RemoveName => QueueCopy.RemoveName(_position);

    /// <summary>Gets the accessible name of the row's Move up control.</summary>
    public string MoveUpName => QueueCopy.MoveUpName(_position);

    /// <summary>Gets the accessible name of the row's Move down control.</summary>
    public string MoveDownName => QueueCopy.MoveDownName(_position);

    /// <summary>Gets the accessible name of the row's Send now control.</summary>
    public string SendNowName => QueueCopy.SendNowName(_position);

    internal void SetText(string text)
    {
        if (SetProperty(ref _text, text, nameof(Text)))
        {
            NotifyPropertyChanged(nameof(PreviewText));
            NotifyPropertyChanged(nameof(RowName));
        }
    }

    internal void SetPlace(int position, int count)
    {
        SetProperty(ref _position, position, nameof(Position));
        SetProperty(ref _canMoveUp, position > 1, nameof(CanMoveUp));
        SetProperty(ref _canMoveDown, position < count, nameof(CanMoveDown));
        NotifyPropertyChanged(nameof(RowName));
        NotifyPropertyChanged(nameof(RemoveName));
        NotifyPropertyChanged(nameof(MoveUpName));
        NotifyPropertyChanged(nameof(MoveDownName));
        NotifyPropertyChanged(nameof(SendNowName));
    }
}
