namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>Runs <c>/logs</c> (<see cref="HandlerActionIds.AppLogs"/>): it opens the folder where OmpOS keeps its logs through <see cref="IFolderOpener"/>.</summary>
public sealed class LogsSlashHandler : ISlashActionHandler
{
    private readonly IFolderOpener _opener;
    private readonly Func<string> _logFolder;

    /// <summary>Creates the handler.</summary>
    /// <param name="opener">Opens a folder in the file manager.</param>
    /// <param name="logFolder">Returns the absolute path of the log folder.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public LogsSlashHandler(IFolderOpener opener, Func<string> logFolder)
    {
        ArgumentNullException.ThrowIfNull(opener);
        ArgumentNullException.ThrowIfNull(logFolder);
        _opener = opener;
        _logFolder = logFolder;
    }

    /// <inheritdoc/>
    public string ActionId => HandlerActionIds.AppLogs;

    /// <inheritdoc/>
    public bool CanRun => true;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var folder = _logFolder();
        return await _opener.OpenAsync(folder, cancellationToken).ConfigureAwait(true)
            ? SlashActionResult.Completed(HandlerCopy.LogsOpened(folder))
            : SlashActionResult.Failed(HandlerCopy.LogsNotOpened(folder));
    }
}
