namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>Shows the About window.</summary>
public interface IAboutLauncher
{
    /// <summary>Shows the About window.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the window is shown.</returns>
    Task ShowAsync(CancellationToken cancellationToken);
}
