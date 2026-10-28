namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>Runs <c>/changes</c> (<see cref="HandlerActionIds.ChangesOpen"/>): it shows the Changes bar of the chat in front, which lists the files the agent changed and offers Keep, Undo and Review for each.</summary>
public sealed class ChangesSlashHandler : ISlashActionHandler
{
    private readonly Func<IChangesBarOpener?> _bar;

    /// <summary>Creates the handler.</summary>
    /// <param name="bar">Returns the Changes bar of the chat in front, or <see langword="null"/> when there is none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bar"/> is null.</exception>
    public ChangesSlashHandler(Func<IChangesBarOpener?> bar)
    {
        ArgumentNullException.ThrowIfNull(bar);
        _bar = bar;
    }

    /// <inheritdoc/>
    public string ActionId => HandlerActionIds.ChangesOpen;

    /// <inheritdoc/>
    public bool CanRun => _bar() is not null;

    /// <inheritdoc/>
    public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bar = _bar();
        if (bar is null)
        {
            return Task.FromResult(SlashActionResult.NotAvailable(HandlerCopy.NotReady));
        }

        if (!bar.HasChanges)
        {
            return Task.FromResult(SlashActionResult.Completed(HandlerCopy.NoChanges));
        }

        bar.Show();
        return Task.FromResult(SlashActionResult.Completed(HandlerCopy.ChangesOpened));
    }
}
