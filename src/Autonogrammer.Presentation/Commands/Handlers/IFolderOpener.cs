namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>Opens a folder in the system's file manager. It is the only way a command touches the file manager, so no model starts a process.</summary>
public interface IFolderOpener
{
    /// <summary>Opens a folder.</summary>
    /// <param name="folder">The absolute path of the folder.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when the file manager opened it.</returns>
    Task<bool> OpenAsync(string folder, CancellationToken cancellationToken);
}
