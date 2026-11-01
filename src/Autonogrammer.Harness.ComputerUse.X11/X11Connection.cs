using System.Collections.Concurrent;

namespace Autonogrammer.Harness.ComputerUse.X11;

/// <summary>
/// One Xlib connection owned by one thread and a command queue. Xlib is not thread-safe and this process
/// does not call XInitThreads. The hotkey registrar opens a second instance and never shares this one.
/// </summary>
internal sealed class X11Connection : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();

    private readonly Thread _thread;

    private readonly ManualResetEventSlim _opened = new(false);

    private IntPtr _display;

    private int _disposed;

    internal X11Connection(string? displayName)
    {
        DisplayName = displayName;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "autonogrammer-x11",
        };
        _thread.Start();
        _opened.Wait();
    }

    internal string? DisplayName { get; }

    internal bool IsOpen => _display != IntPtr.Zero;

    internal Task InvokeAsync(Action<IntPtr> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return InvokeAsync(
            display =>
            {
                action(display);
                return 0;
            },
            cancellationToken);
    }

    internal Task<T> InvokeAsync<T>(Func<IntPtr, T> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId)
        {
            return Task.FromResult(action(_display));
        }

        var task = new Task<T>(() => action(_display), CancellationToken.None);
        _queue.Add(() => task.RunSynchronously(), cancellationToken);
        return task;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(2));
        _opened.Dispose();
        _queue.Dispose();
    }

    private void Run()
    {
        _display = X11Interop.Open(DisplayName);
        _opened.Set();
        try
        {
            foreach (Action work in _queue.GetConsumingEnumerable())
            {
                work();
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        if (_display != IntPtr.Zero)
        {
            try
            {
                _ = X11Interop.CloseDisplay(_display);
            }
            catch (IOException)
            {
            }

            _display = IntPtr.Zero;
        }
    }
}
