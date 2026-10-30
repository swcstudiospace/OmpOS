using Autonogrammer.Presentation.SideChats;

namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>Runs <c>/side</c> and <c>/btw</c> (<see cref="SlashActionIds.SideOpen"/>) over the session's <see cref="SideChatHub"/>: it opens a side chat seeded with a read-only summary of the chat and asks the typed question, which the engine answers.</summary>
public sealed class SideChatSlashHandler : ISlashActionHandler
{
    private readonly Func<SideChatHub?> _hub;

    /// <summary>Creates the handler.</summary>
    /// <param name="hub">Returns the side chat hub of the chat in front, or <see langword="null"/> when there is none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hub"/> is null.</exception>
    public SideChatSlashHandler(Func<SideChatHub?> hub)
    {
        ArgumentNullException.ThrowIfNull(hub);
        _hub = hub;
    }

    /// <inheritdoc/>
    public string ActionId => SlashActionIds.SideOpen;

    /// <inheritdoc/>
    public bool CanRun => _hub() is not null;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hub = _hub();
        if (hub is null)
        {
            return SlashActionResult.NotAvailable(HandlerCopy.NotReady);
        }

        var chat = await hub.OpenAsync(request.TypedText.Trim(), cancellationToken).ConfigureAwait(true);
        return chat.HasError ? SlashActionResult.Failed(chat.StatusText) : SlashActionResult.Completed(request.TypedText.Trim().Length == 0 ? "Opened a side chat." : string.Empty);
    }
}
