namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>Runs <c>/quit</c> and <c>/exit</c> (<see cref="HandlerActionIds.AppQuit"/>): it asks the application to shut down through <see cref="IApplicationLifetime"/>, which asks about unsaved work and running agents first.</summary>
public sealed class QuitSlashHandler : ISlashActionHandler
{
    private readonly IApplicationLifetime _lifetime;

    /// <summary>Creates the handler.</summary>
    /// <param name="lifetime">Closes the application.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lifetime"/> is null.</exception>
    public QuitSlashHandler(IApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        _lifetime = lifetime;
    }

    /// <inheritdoc/>
    public string ActionId => HandlerActionIds.AppQuit;

    /// <inheritdoc/>
    public bool CanRun => true;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _lifetime.RequestShutdownAsync(cancellationToken).ConfigureAwait(true);
        return SlashActionResult.Completed(HandlerCopy.Quitting);
    }
}
