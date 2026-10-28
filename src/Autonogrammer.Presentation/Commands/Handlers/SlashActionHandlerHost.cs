namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>
/// The <see cref="ISlashActionHost"/> that runs the actions of <see cref="ISlashActionHandler"/>s and passes every other action to the head's own host. Handlers win over the fallback for the same id,
/// so a handler can replace an action the head runs only in part (the side chat). An exception that escapes a handler is reported as a failed result, never thrown into the palette.
/// </summary>
public sealed class SlashActionHandlerHost : ISlashActionHost
{
    private readonly Dictionary<string, ISlashActionHandler> _handlers = new(StringComparer.Ordinal);
    private readonly ISlashActionHost? _fallback;

    /// <summary>Creates the host.</summary>
    /// <param name="handlers">The handlers. Each action id may have one.</param>
    /// <param name="fallback">The head's own host for every id that has no handler, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handlers"/> is null.</exception>
    /// <exception cref="ArgumentException">Two handlers have the same action id.</exception>
    public SlashActionHandlerHost(IEnumerable<ISlashActionHandler> handlers, ISlashActionHost? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        foreach (var handler in handlers)
        {
            if (!_handlers.TryAdd(handler.ActionId, handler))
            {
                throw new ArgumentException("Two handlers run the action " + handler.ActionId + ".", nameof(handlers));
            }
        }

        _fallback = fallback;
    }

    /// <inheritdoc/>
    public bool CanRun(string actionId)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        return _handlers.TryGetValue(actionId, out var handler) ? handler.CanRun : _fallback?.CanRun(actionId) ?? false;
    }

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_handlers.TryGetValue(request.ActionId, out var handler))
        {
            return _fallback is null
                ? SlashActionResult.NotAvailable(SlashPaletteCopy.ActionNotInThisWindow)
                : await _fallback.RunAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (!handler.CanRun)
        {
            return SlashActionResult.NotAvailable(HandlerCopy.NotReady);
        }

        try
        {
            return await handler.RunAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            return SlashActionResult.Failed("The command failed: " + exception.Message);
        }
    }
}
