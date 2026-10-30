using Autonogrammer.Presentation.Commands.Handlers;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace Autonogrammer.Native.Views.Shell;

/// <summary>The clipboard the copy commands write to: the clipboard of the window the view is shown in.</summary>
/// <param name="topLevel">Gives the window, or <see langword="null"/> while the view is not shown.</param>
internal sealed class ClipboardWriter(Func<TopLevel?> topLevel) : IClipboardWriter
{
    /// <inheritdoc/>
    public async Task<bool> SetTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        var clipboard = topLevel()?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        try
        {
            await clipboard.SetTextAsync(text).ConfigureAwait(true);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            return false;
        }
    }
}
