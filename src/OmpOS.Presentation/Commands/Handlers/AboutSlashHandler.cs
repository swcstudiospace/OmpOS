namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>Runs <c>/about</c> (<see cref="HandlerActionIds.AppAbout"/>): it shows the About window through <see cref="IAboutLauncher"/>.</summary>
public sealed class AboutSlashHandler : ISlashActionHandler
{
    private readonly IAboutLauncher _launcher;

    /// <summary>Creates the handler.</summary>
    /// <param name="launcher">Shows the About window.</param>
    /// <exception cref="ArgumentNullException"><paramref name="launcher"/> is null.</exception>
    public AboutSlashHandler(IAboutLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        _launcher = launcher;
    }

    /// <inheritdoc/>
    public string ActionId => HandlerActionIds.AppAbout;

    /// <inheritdoc/>
    public bool CanRun => true;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _launcher.ShowAsync(cancellationToken).ConfigureAwait(true);
        return SlashActionResult.Completed();
    }
}
