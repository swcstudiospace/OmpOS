using Avalonia.Controls;
using Avalonia.Threading;
using OmpOS.Presentation.Commands.Handlers;

namespace OmpOS.Native.Views.Shell;

/// <summary>Quits the application by closing its main window, which runs the head's ordinary shutdown (the sessions stop first).</summary>
/// <param name="topLevel">Gives the window, or <see langword="null"/> while the view is not shown.</param>
internal sealed class WindowLifetime(Func<TopLevel?> topLevel) : IApplicationLifetime
{
    /// <inheritdoc/>
    public Task RequestShutdownAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Dispatcher.UIThread.Post(() => (topLevel() as Window)?.Close());
        return Task.CompletedTask;
    }
}
