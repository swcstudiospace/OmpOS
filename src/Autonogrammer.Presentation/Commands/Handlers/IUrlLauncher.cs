namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>Opens an address in the user's browser. Only <c>https</c> addresses are passed to it.</summary>
public interface IUrlLauncher
{
    /// <summary>Opens an address.</summary>
    /// <param name="url">The address.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when the browser was started.</returns>
    Task<bool> OpenAsync(Uri url, CancellationToken cancellationToken);
}
