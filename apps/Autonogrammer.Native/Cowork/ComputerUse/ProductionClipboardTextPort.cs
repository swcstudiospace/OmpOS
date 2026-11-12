using Autonogrammer.Harness.ComputerUse.Abstractions;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace Autonogrammer.Native.Cowork.ComputerUse;

/// <summary>
/// Head clipboard over the Avalonia clipboard of the window the head shows. Clipboard calls run on the UI thread.
/// With no window, or a window without a clipboard, every call throws <see cref="ComputerBackendException"/> with
/// <see cref="ComputerFailure.Unavailable"/>. Nothing is caught: a platform failure reaches the caller.
/// </summary>
internal sealed class ProductionClipboardTextPort : IClipboardTextPort
{
    private readonly Func<TopLevel?> _topLevel;

    /// <summary>Creates the port.</summary>
    /// <param name="topLevel">Returns the window whose clipboard is used, or null when none is shown.</param>
    /// <exception cref="ArgumentNullException"><paramref name="topLevel"/> is null.</exception>
    public ProductionClipboardTextPort(Func<TopLevel?> topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _topLevel = topLevel;
    }

    /// <inheritdoc/>
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Func<Task<string?>> read = () => ClipboardOrThrow().TryGetTextAsync();
        Task<string?> pending = Dispatcher.UIThread.InvokeAsync(read);
        return new ValueTask<string?>(pending.WaitAsync(cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        Func<Task> write = () => ClipboardOrThrow().SetTextAsync(text);
        Task pending = Dispatcher.UIThread.InvokeAsync(write);
        return new ValueTask(pending.WaitAsync(cancellationToken));
    }

    private IClipboard ClipboardOrThrow() =>
        _topLevel()?.Clipboard ?? throw new ComputerBackendException(ComputerFailure.Unavailable);
}
