using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Shell;

/// <summary>Gives the <c>/about</c> handler the head's About launcher, centred over the main window.</summary>
/// <param name="inner">The head's launcher.</param>
/// <param name="owner">Gives the window to centre over, or <see langword="null"/>.</param>
internal sealed class HandlerAboutLauncher(IAboutLauncher inner, Func<Window?> owner) : Autonogrammer.Presentation.Commands.Handlers.IAboutLauncher
{
    /// <inheritdoc/>
    public Task ShowAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        inner.Show(owner());
        return Task.CompletedTask;
    }
}
