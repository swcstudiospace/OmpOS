using Avalonia.Controls;
using Avalonia.Input.Platform;
using OmpOS.Presentation.About;

namespace OmpOS.Native.Views;

/// <summary>The About window's <see cref="IAboutClipboard"/> over the clipboard of the window that shows it.</summary>
internal sealed class AvaloniaAboutClipboard : IAboutClipboard
{
    private readonly Func<TopLevel?> _topLevel;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaAboutClipboard"/> class.</summary>
    /// <param name="topLevel">Gives the window whose clipboard is used. It may return null while the window is closed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="topLevel"/> is null.</exception>
    public AvaloniaAboutClipboard(Func<TopLevel?> topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _topLevel = topLevel;
    }

    /// <inheritdoc/>
    public async Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        var clipboard = _topLevel()?.Clipboard ?? throw new InvalidOperationException("The window has no clipboard.");
        await clipboard.SetTextAsync(text).ConfigureAwait(true);
    }
}
