namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>
/// Runs the copy commands: <c>/copy</c> (<see cref="HandlerActionIds.ChatCopy"/>) copies the assistant's last response, or with the word <c>user</c> the last message the user sent;
/// <c>/copy-conversation-id</c> copies the session id and <c>/copy-request-id</c> the id of the last request. Nothing is copied when there is nothing to copy, and the status line says so.
/// </summary>
public sealed class CopySlashHandler : ISlashActionHandler
{
    private readonly ISlashSessionContext _session;
    private readonly IClipboardWriter _clipboard;

    /// <summary>Creates the handler of one action.</summary>
    /// <param name="actionId">One of <see cref="HandlerActionIds.ChatCopy"/>, <see cref="HandlerActionIds.ChatCopyConversationId"/> and <see cref="HandlerActionIds.ChatCopyRequestId"/>.</param>
    /// <param name="session">What to copy.</param>
    /// <param name="clipboard">Where to copy it.</param>
    /// <exception cref="ArgumentException"><paramref name="actionId"/> is not a copy action.</exception>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public CopySlashHandler(string actionId, ISlashSessionContext session, IClipboardWriter clipboard)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(clipboard);
        if (actionId is not (HandlerActionIds.ChatCopy or HandlerActionIds.ChatCopyConversationId or HandlerActionIds.ChatCopyRequestId))
        {
            throw new ArgumentException("Not a copy action: " + actionId, nameof(actionId));
        }

        ActionId = actionId;
        _session = session;
        _clipboard = clipboard;
    }

    /// <inheritdoc/>
    public string ActionId { get; }

    /// <inheritdoc/>
    public bool CanRun => true;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (text, what, missing) = Choose(request.TypedText.Trim());
        if (string.IsNullOrWhiteSpace(text))
        {
            return SlashActionResult.Failed(missing);
        }

        return await _clipboard.SetTextAsync(text, cancellationToken).ConfigureAwait(true)
            ? SlashActionResult.Completed(HandlerCopy.Copied(what))
            : SlashActionResult.Failed(HandlerCopy.ClipboardRefused());
    }

    private (string? Text, string What, string Missing) Choose(string typed) => ActionId switch
    {
        HandlerActionIds.ChatCopyConversationId => (_session.SessionId, "the conversation id", HandlerCopy.NoSessionId),
        HandlerActionIds.ChatCopyRequestId => (_session.LastRequestId, "the request id", HandlerCopy.NoRequestId),
        _ when string.Equals(typed, "user", StringComparison.OrdinalIgnoreCase) => (_session.LastUserMessageText, "your last message", HandlerCopy.NothingToCopy),
        _ => (_session.LastResponseText, "the last response", HandlerCopy.NothingToCopy),
    };
}
