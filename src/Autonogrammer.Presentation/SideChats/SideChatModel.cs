using Autonogrammer.Presentation.Chats;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.SideChats;

/// <summary>
/// A side chat (<c>/side</c>, <c>/btw</c>): a child conversation that sees a read-only summary of its parent session and closes on its own. The user's questions and the assistant's answers are kept in the
/// side chat's own <see cref="Chat"/>; nothing is added to the parent's transcript, and closing never touches the parent. A side chat cannot open another side chat.
/// </summary>
/// <remarks>Not thread-safe: it runs on the UI context that owns the presentation models.</remarks>
public sealed class SideChatModel : ObservableModel
{
    private readonly ISideChatEngine _engine;
    private bool _isClosed;
    private bool _isAnswering;
    private bool _hasError;
    private string _statusText = string.Empty;

    /// <summary>Creates a side chat.</summary>
    /// <param name="chat">The conversation that holds the messages; it carries the parent session id and the title.</param>
    /// <param name="parentSummary">The read-only summary of the parent, taken when the side chat was opened.</param>
    /// <param name="engine">Answers the questions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chat"/>, <paramref name="parentSummary"/> or <paramref name="engine"/> is null.</exception>
    public SideChatModel(SideChat chat, string parentSummary, ISideChatEngine engine)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(parentSummary);
        ArgumentNullException.ThrowIfNull(engine);
        Chat = chat;
        ParentSummary = parentSummary;
        _engine = engine;
    }

    /// <summary>Gets the side chat's own conversation.</summary>
    public SideChat Chat { get; }

    /// <summary>Gets the identifier of the side chat.</summary>
    public string Id => Chat.Id;

    /// <summary>Gets the label shown for the side chat.</summary>
    public string Title => Chat.Title;

    /// <summary>Gets the session the side chat was opened from.</summary>
    public string ParentSessionId => Chat.ParentSessionId;

    /// <summary>Gets the read-only summary of the parent that the engine sees. It is hidden context and not a message of the chat.</summary>
    public string ParentSummary { get; }

    /// <summary>Gets whether the side chat is closed. A closed side chat takes no question.</summary>
    public bool IsClosed => _isClosed;

    /// <summary>Gets whether the assistant is answering.</summary>
    public bool IsAnswering => _isAnswering;

    /// <summary>Gets whether this side chat may open another: always <see langword="false"/>, because side chats do not nest.</summary>
    public bool CanOpenSideChat { get; }

    /// <summary>Gets the last sentence for the status line and assistive technology.</summary>
    public string StatusText => _statusText;

    /// <summary>Gets whether <see cref="StatusText"/> reports a failure or a refusal.</summary>
    public bool HasError => _hasError;

    /// <summary>Asks the assistant a question. The question appears at once; the answer is added when the engine returns it.</summary>
    /// <param name="question">The question.</param>
    /// <param name="cancellationToken">Cancels the question.</param>
    /// <returns><see langword="true"/> when the assistant answered. A closed chat, a blank question, a question while another is being answered, or an engine failure return <see langword="false"/> with the reason in <see cref="StatusText"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is null.</exception>
    public async Task<bool> AskAsync(string question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        if (_isClosed)
        {
            SetStatus(SideChatCopy.ClosedRefusal, isError: true);
            return false;
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            SetStatus(SideChatCopy.BlankQuestion, isError: true);
            return false;
        }

        if (_isAnswering)
        {
            SetStatus(SideChatCopy.Answering, isError: false);
            return false;
        }

        var history = Chat.Messages;
        Chat.Post(SideChatAuthor.User, question.Trim());
        SetAnswering(true);
        SetStatus(SideChatCopy.Answering, isError: false);
        try
        {
            var answer = await _engine.AskAsync(new SideChatRequest(Id, ParentSessionId, ParentSummary, history, question.Trim()), cancellationToken).ConfigureAwait(true);
            if (_isClosed)
            {
                return false;
            }

            Chat.Post(SideChatAuthor.Assistant, string.IsNullOrWhiteSpace(answer) ? "(no answer)" : answer);
            SetStatus(string.Empty, isError: false);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            SetStatus(SideChatCopy.Failed(exception.Message), isError: true);
            return false;
        }
        finally
        {
            SetAnswering(false);
        }
    }

    /// <summary>Closes the side chat and ends its child conversation. The parent session is not touched.</summary>
    /// <param name="cancellationToken">Cancels the close of the child conversation.</param>
    /// <returns>A task that completes when the side chat is closed. Closing twice does nothing.</returns>
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        if (_isClosed)
        {
            return;
        }

        SetProperty(ref _isClosed, true, nameof(IsClosed));
        try
        {
            await _engine.CloseAsync(Id, cancellationToken).ConfigureAwait(true);
            SetStatus(SideChatCopy.Closed, isError: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            SetStatus(SideChatCopy.Closed + " The engine reported: " + exception.Message.TrimEnd('.') + ".", isError: true);
        }
    }

    private void SetAnswering(bool answering) => SetProperty(ref _isAnswering, answering, nameof(IsAnswering));

    private void SetStatus(string text, bool isError)
    {
        SetProperty(ref _statusText, text, nameof(StatusText));
        SetProperty(ref _hasError, isError, nameof(HasError));
    }
}
