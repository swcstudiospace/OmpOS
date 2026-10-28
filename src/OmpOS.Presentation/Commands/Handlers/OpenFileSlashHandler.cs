using System.Globalization;
using OmpOS.Presentation.Editor;

namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>
/// Runs <c>/open path[:line]</c> (<see cref="HandlerActionIds.EditorOpen"/>): it opens a workspace file in the editor through <see cref="IEditorOpener"/>. The path is relative to the workspace root and is normalised with
/// <see cref="WorkspacePath"/> first, so a path that climbs out of the workspace, an absolute path and an empty path are refused before the editor is asked.
/// </summary>
public sealed class OpenFileSlashHandler : ISlashActionHandler
{
    private readonly IEditorOpener _opener;

    /// <summary>Creates the handler.</summary>
    /// <param name="opener">Opens a workspace file in the editor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="opener"/> is null.</exception>
    public OpenFileSlashHandler(IEditorOpener opener)
    {
        ArgumentNullException.ThrowIfNull(opener);
        _opener = opener;
    }

    /// <inheritdoc/>
    public string ActionId => HandlerActionIds.EditorOpen;

    /// <inheritdoc/>
    public bool CanRun => true;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var typed = request.TypedText.Trim();
        if (typed.Length == 0)
        {
            return SlashActionResult.Failed(HandlerCopy.OpenNeedsPath);
        }

        var (rawPath, line) = SplitLine(typed);
        string path;
        try
        {
            path = WorkspacePath.Normalize(rawPath);
        }
        catch (ArgumentException)
        {
            return SlashActionResult.Failed(HandlerCopy.OpenOutside);
        }

        if (path.Length == 0)
        {
            return SlashActionResult.Failed(HandlerCopy.OpenNeedsPath);
        }

        return await _opener.OpenAsync(path, line, cancellationToken).ConfigureAwait(true)
            ? SlashActionResult.Completed(HandlerCopy.FileOpened(path, line))
            : SlashActionResult.Failed(HandlerCopy.FileNotOpened(path));
    }

    private static (string Path, int? Line) SplitLine(string typed)
    {
        var colon = typed.LastIndexOf(':');
        if (colon > 0 && int.TryParse(typed[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var line) && line >= 1)
        {
            return (typed[..colon], line);
        }

        return (typed, null);
    }
}
