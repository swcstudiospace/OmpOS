using Avalonia.Controls;

namespace OmpOS.Native.Views;

/// <summary>The real <see cref="IUrlLauncher"/>: it asks the platform launcher of a window, which opens the user's default browser.</summary>
public sealed class SystemUrlLauncher : IUrlLauncher
{
    private readonly Func<TopLevel?> _topLevel;

    /// <summary>Initializes a new instance of the <see cref="SystemUrlLauncher"/> class.</summary>
    /// <param name="topLevel">Gives the window whose platform launcher is used. It may return null while the window is closed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="topLevel"/> is null.</exception>
    public SystemUrlLauncher(Func<TopLevel?> topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _topLevel = topLevel;
    }

    /// <inheritdoc/>
    public async Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        cancellationToken.ThrowIfCancellationRequested();
        var launcher = _topLevel()?.Launcher;
        return launcher is not null && await launcher.LaunchUriAsync(uri).ConfigureAwait(true);
    }
}
