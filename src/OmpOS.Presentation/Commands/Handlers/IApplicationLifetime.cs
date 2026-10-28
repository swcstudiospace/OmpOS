namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>Closes the application. The head implements it as a click on the window's close button: it asks about unsaved work and running agents first, so <c>/quit</c> never loses what closing the window would not.</summary>
public interface IApplicationLifetime
{
    /// <summary>Asks the application to shut down.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the request was made.</returns>
    Task RequestShutdownAsync(CancellationToken cancellationToken);
}
